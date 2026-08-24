using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.PocketMill;
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
        /// Медленный генератор: спит ~300 мс, затем делегирует реальному
        /// SimpleGCodeGenerator. Если команда выполнялась бы на потоке вызывающего
        /// (UI), Execute() заняла бы не менее 300 мс.
        /// </summary>
        private sealed class SlowGCodeGenerator : IGCodeGenerator
        {
            private readonly SimpleGCodeGenerator _inner = new SimpleGCodeGenerator();

            public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
            {
                Thread.Sleep(300);
                return _inner.Generate(operations, settings, progress);
            }
        }

        /// <summary>Синхронный IProgress&lt;int&gt; для детерминированных проверок (без marshalling).</summary>
        private sealed class ListProgress : IProgress<int>
        {
            public List<int> Values { get; } = new List<int>();

            public void Report(int value) => Values.Add(value);
        }

        /// <summary>Заглушка IDialogService: «выбирает» файл для импорта (без окон).</summary>
        private sealed class StubDialogService : IDialogService
        {
            public string OpenDialogResult { get; set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "") { }
            public bool ShowConfirm(string message, string title = "") => true;
            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => OpenDialogResult;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "") => null;
            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class => throw new NotSupportedException();
            public object CreateViewModel(Type viewModelType) => throw new NotSupportedException();
            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class => throw new NotSupportedException();
            public void ShowDialog(Type viewModelType, object viewModel) => throw new NotSupportedException();
        }

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
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(new SlowGCodeGenerator());
            main.AllOperations.Add(CreateDrillOperation("Drill1"));

            var command = (IAsyncRelayCommand)main.GenerateGCodeCommand;
            var stopwatch = Stopwatch.StartNew();
            var task = command.ExecuteAsync(null);
            stopwatch.Stop();

            // Пункт 8.4 плана: генерация выполняется в пуле (Task.Run), поток
            // вызывающего (UI) не блокируется — Execute() возвращается сразу,
            // хотя полная генерация занимает не менее 300 мс.
            Assert.IsFalse(task.IsCompleted, "Команда не должна завершиться синхронно");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 250,
                $"Execute() должен вернуться сразу, занял {stopwatch.ElapsedMilliseconds} мс");

            await task;

            Assert.IsFalse(main.IsGenerating, "После завершения генерации IsGenerating == false");
            Assert.AreEqual(100, main.ProgressPercent, "После завершения ProgressPercent == 100");
            Assert.IsFalse(string.IsNullOrEmpty(main.GCodePreview), "G-код должен быть сгенерирован");
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
            const int lineCount = 12000;
            var path = Path.Combine(Path.GetTempPath(), $"gcodegen_big_dxf_{Guid.NewGuid():N}.dxf");
            try
            {
                WriteBigDxf(path, lineCount);

                var dialogService = new StubDialogService { OpenDialogResult = path };
                var vm = new ProfileDxfOperationViewModel(null, dialogService, new DxfImportService());

                var stopwatch = Stopwatch.StartNew();
                var task = ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);
                stopwatch.Stop();

                Assert.IsFalse(task.IsCompleted, "Импорт не должен завершиться синхронно");
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 250,
                    $"Execute() должен вернуться сразу, занял {stopwatch.ElapsedMilliseconds} мс");

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

        /// <summary>
        /// Генерирует DXF с N сущностями LINE в формате, который ожидает парсер
        /// (пары «код/значение», имя сущности — отдельной строкой).
        /// </summary>
        private static void WriteBigDxf(string path, int lineCount)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
            for (var i = 0; i < lineCount; i++)
            {
                writer.WriteLine("0");
                writer.WriteLine("LINE");
                writer.WriteLine("8");
                writer.WriteLine("0");
                writer.WriteLine("10");
                writer.WriteLine((i * 0.5).ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("20");
                writer.WriteLine("0");
                writer.WriteLine("11");
                writer.WriteLine((i * 0.5 + 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteLine("21");
                writer.WriteLine("5");
            }
        }
    }
}
