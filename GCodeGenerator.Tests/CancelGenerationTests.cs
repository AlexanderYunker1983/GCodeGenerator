using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Построение программы можно прервать из окна.
    ///
    /// Отмена работала и прежде, но включалась только правкой документа:
    /// на большом проекте с мелким шагом выборки оставалось ждать, даже когда
    /// уже видно, что параметры заданы не те. Токен при этом доходил до
    /// каждого слоя и отверстия — не хватало только команды в окне.
    /// </summary>
    [TestClass]
    public class CancelGenerationTests
    {
        /// <summary>
        /// Генератор, который стоит, пока его не отменят. Так ведёт себя
        /// долгая выборка: работа идёт, и прервать её может только токен.
        /// </summary>
        private sealed class CancellableGenerator : IGCodeGenerator
        {
            private readonly SimpleGCodeGenerator _inner = new SimpleGCodeGenerator();

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(false);

            public GCodeProgram Generate(
                IReadOnlyList<OperationBase> operations, GCodeSettings settings,
                IProgress<int> progress = null, CancellationToken cancellation = default)
                => _inner.Generate(operations, settings, progress, cancellation);

            public GCodeGenerator.Toolpath.ToolPath BuildToolPath(
                IReadOnlyList<OperationBase> operations, GCodeSettings settings,
                IProgress<int> progress = null, CancellationToken cancellation = default)
            {
                Started.Set();
                cancellation.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                cancellation.ThrowIfCancellationRequested();
                return _inner.BuildToolPath(operations, settings, progress, cancellation);
            }
        }

        private static DrillPointsOperation Operation() => new DrillPointsOperation
        {
            Name = "Drill",
            Holes = { new DrillHole { X = 10, Y = 20, Z = 0, TotalDepth = 2, StepDepth = 1 } },
        };

        /// <summary>Отменять нечего, пока ничего не строится.</summary>
        [TestMethod]
        public void Cancel_IsUnavailable_WhileNothingIsGenerating()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();

            Assert.IsFalse(main.GCodeWorkflow.CancelGenerationCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cancel_StopsTheGeneration()
        {
            var generator = new CancellableGenerator();
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(generator);
            main.OperationsWorkspace.AllOperations.Add(Operation());
            var workflow = main.GCodeWorkflow;

            var task = ((IAsyncRelayCommand)workflow.GenerateGCodeCommand).ExecuteAsync(null);
            Assert.IsTrue(generator.Started.Wait(TimeSpan.FromSeconds(2)), "Генерация должна начаться");
            Assert.IsTrue(workflow.IsGenerating, "Пока строится — идёт генерация");
            Assert.IsTrue(workflow.CancelGenerationCommand.CanExecute(null),
                "Во время генерации её можно прервать");

            workflow.CancelGenerationCommand.Execute(null);
            await task;

            Assert.IsFalse(workflow.IsGenerating, "После отмены генерация не идёт");
            Assert.AreEqual(0, workflow.ProgressPercent, "Ход сброшен");
            Assert.IsNull(workflow.ProgramLines, "Программы нет");
            Assert.IsNull(workflow.GeneratedToolPath, "Траектории нет");
            Assert.AreEqual(0, dialogs.ErrorMessageCount, "Отмена — не ошибка, сообщать не о чем");
        }

        /// <summary>После остановки отменять снова нечего.</summary>
        [TestMethod]
        public async Task Cancel_IsUnavailableAgain_AfterTheGenerationStops()
        {
            var generator = new CancellableGenerator();
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(generator);
            main.OperationsWorkspace.AllOperations.Add(Operation());
            var workflow = main.GCodeWorkflow;

            var task = ((IAsyncRelayCommand)workflow.GenerateGCodeCommand).ExecuteAsync(null);
            generator.Started.Wait(TimeSpan.FromSeconds(2));
            workflow.CancelGenerationCommand.Execute(null);
            await task;

            Assert.IsFalse(workflow.CancelGenerationCommand.CanExecute(null));
        }

        /// <summary>
        /// Прерванное построение не мешает следующему: после отмены программа
        /// строится заново и доходит до конца.
        /// </summary>
        [TestMethod]
        public async Task Generation_AfterCancel_RunsToCompletion()
        {
            var generator = new CancellableGenerator();
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(generator);
            main.OperationsWorkspace.AllOperations.Add(Operation());
            var workflow = main.GCodeWorkflow;

            var cancelled = ((IAsyncRelayCommand)workflow.GenerateGCodeCommand).ExecuteAsync(null);
            generator.Started.Wait(TimeSpan.FromSeconds(2));
            workflow.CancelGenerationCommand.Execute(null);
            await cancelled;

            // Второй заход идёт обычным генератором: важно, что рабочий
            // процесс после отмены снова готов строить.
            var (again, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            again.OperationsWorkspace.AllOperations.Add(Operation());
            await ((IAsyncRelayCommand)again.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);

            Assert.AreEqual(100, again.GCodeWorkflow.ProgressPercent);
            Assert.IsFalse(string.IsNullOrEmpty(again.GCodeWorkflow.GCodePreview));
        }
    }
}
