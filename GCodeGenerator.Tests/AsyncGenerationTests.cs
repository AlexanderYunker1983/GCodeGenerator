using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels.PocketMill;
using netDxf;
using netDxf.Header;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты пункта 8.4 плана: async-генерация G-кода с прогрессом (UI-поток
    /// не блокируется, работа — в пуле через Task.Run), отчёт о прогрессе в
    /// Core (по операциям) и неблокирующий импорт большого DXF-файла.
    /// </summary>
    [TestClass]
    public class AsyncGenerationTests
    {
        /// <summary>
        /// Генератор со шлюзом: сообщает о старте и стоит, пока тест его не
        /// отпустит. Прежний вариант спал 300 мс, а тест мерил секундомером
        /// «Execute вернулся быстрее 250 мс» — на занятой машине порог ложно
        /// срабатывал, а на быстрой почти не отличал фон от блокировки.
        /// Сигналы доказывают то же без часов: вызов вернулся, а генерация
        /// ещё стоит на шлюзе — значит, вызывающий поток её не ждал.
        /// </summary>
        private sealed class GatedGCodeGenerator : IGCodeGenerator
        {
            private readonly SimpleGCodeGenerator _inner = new SimpleGCodeGenerator();

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

            public GCodeProgram Generate(IReadOnlyList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null,
                CancellationToken cancellation = default)
            {
                Started.Set();
                Release.Wait(TimeSpan.FromSeconds(5));
                return _inner.Generate(operations, settings, progress);
            }

            /// <summary>Траектория тесту не нужна: проверяется работа с программой.</summary>
            public GCodeGenerator.Toolpath.ToolPath BuildToolPath(
                IReadOnlyList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null,
                CancellationToken cancellation = default)
            {
                Started.Set();
                Release.Wait(TimeSpan.FromSeconds(5));
                return _inner.BuildToolPath(operations, settings, progress);
            }
        }

        /// <summary>Синхронный IProgress&lt;int&gt; для детерминированных проверок (без marshalling).</summary>
        private sealed class ListProgress : IProgress<int>
        {
            public List<int> Values { get; } = new List<int>();

            public void Report(int value) => Values.Add(value);
        }

        /// <summary>Заглушка IDialogService: «выбирает» файл для импорта (без окон).</summary>
        private static DrillPointsOperation CreateDrillOperation(string name)
        {
            return new DrillPointsOperation
            {
                Name = name,
                Holes = { new DrillHole { X = 10, Y = 20, Z = 0, TotalDepth = 2, StepDepth = 1 } }
            };
        }

        [TestMethod]
        public async Task GenerateGCodeCommand_DoesNotBlockCaller_AndCompletes()
        {
            var generator = new GatedGCodeGenerator();
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(generator);
            main.OperationsWorkspace.AllOperations.Add(CreateDrillOperation("Drill1"));

            var command = (IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand;
            var task = command.ExecuteAsync(null);

            // Пункт 8.4 плана: генерация выполняется в пуле (Task.Run), поток
            // вызывающего (UI) не блокируется. Вызов уже вернулся, а генератор
            // ещё стоит на шлюзе — блокирующая реализация не вернулась бы,
            // пока генерация не кончится.
            Assert.IsTrue(generator.Started.Wait(TimeSpan.FromSeconds(2)), "Генерация должна начаться в фоне");
            Assert.IsFalse(task.IsCompleted, "Команда не должна завершиться синхронно");

            generator.Release.Set();
            await task;

            Assert.IsFalse(main.GCodeWorkflow.IsGenerating, "После завершения генерации IsGenerating == false");
            Assert.AreEqual(100, main.GCodeWorkflow.ProgressPercent, "После завершения ProgressPercent == 100");
            Assert.IsFalse(string.IsNullOrEmpty(main.GCodeWorkflow.GCodePreview), "G-код должен быть сгенерирован");
        }

        [TestMethod]
        public void SimpleGCodeGenerator_ReportsProgress_PerOperation()
        {
            var operations = new List<OperationBase>
            {
                CreateDrillOperation("Drill1"),
                CreateDrillOperation("Drill2")
            };
            var progress = new ListProgress();

            var program = new SimpleGCodeGenerator().Generate(operations, new GCodeSettings(), progress);

            // Пункт 8.4 плана: прогресс по операциям — (index+1)*100/total.
            CollectionAssert.AreEqual(new[] { 50, 100 }, progress.Values);
            Assert.IsTrue(program.Lines.Count > 0, "Программа не пуста");
        }

        [TestMethod]
        public async Task ImportDxfCommand_LargeFile_DoesNotBlockCaller_AndParsesAllSegments()
        {
            // DoD пункта 8.4: большой DXF (>10k сегментов) не блокирует UI —
            // парсинг выполняется в пуле (Task.Run) внутри AsyncRelayCommand.
            // Неблокирование доказывается шлюзом, а не секундомером: вызов
            // вернулся, пока чтение ещё стоит на сигнале.
            const int lineCount = 12000;
            var path = Path.Combine(Path.GetTempPath(), $"gcodegen_big_dxf_{Guid.NewGuid():N}.dxf");
            try
            {
                WriteBigDxf(path, lineCount);

                var dialogs = new FakeDialogs { OpenDialogResult = path };
                var import = new GatedDxfImportService();
                var vm = new ProfileDxfOperationViewModel(null, dialogs, dialogs, import);

                var task = ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

                Assert.IsTrue(import.Started.Wait(TimeSpan.FromSeconds(2)), "Чтение должно начаться в фоне");
                Assert.IsFalse(task.IsCompleted, "Импорт не должен завершиться синхронно");

                import.Release.Set();
                await task;

                Assert.AreEqual(lineCount, vm.Operation.Polylines.Count, "Все сущности LINE распознаны");
                var segments = vm.Operation.Polylines.Sum(p => Math.Max(0, p.Points.Count - 1));
                Assert.IsTrue(segments > 10000, $"Ожидается >10000 сегментов, получено {segments}");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Служба импорта со шлюзом: см. <see cref="GatedGCodeGenerator"/>.</summary>
        private sealed class GatedDxfImportService : IDxfImportService
        {
            private readonly DxfImportService _inner = new DxfImportService();

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

            public List<Polyline2D> ReadProfilePolylines(string path, CancellationToken cancellation = default)
            {
                Started.Set();
                Release.Wait(TimeSpan.FromSeconds(5), cancellation);
                return _inner.ReadProfilePolylines(path, cancellation);
            }

            public List<Polyline2D> ReadPocketClosedContours(string path, CancellationToken cancellation = default)
            {
                Started.Set();
                Release.Wait(TimeSpan.FromSeconds(5));
                return _inner.ReadPocketClosedContours(path, cancellation);
            }
        }

        /// <summary>
        /// Генерирует полноценный DXF-чертёж с N отрезками — такой же, какой
        /// приходит из CAD-системы: с шапкой, таблицами и секцией сущностей.
        /// </summary>
        private static void WriteBigDxf(string path, int lineCount)
        {
            var document = new DxfDocument(DxfVersion.AutoCad2000);
            document.DrawingVariables.InsUnits = netDxf.Units.DrawingUnits.Millimeters;
            for (var i = 0; i < lineCount; i++)
            {
                document.Entities.Add(new netDxf.Entities.Line(
                    new Vector2(i * 0.5, 0),
                    new Vector2(i * 0.5 + 1, 5)));
            }
            document.Save(path);
        }
    }
}
