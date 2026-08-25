using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class GeneratedProgramInvalidationTests
    {
        private sealed class PausingGenerator : IGCodeGenerator, IDisposable
        {
            private readonly ManualResetEventSlim _started = new ManualResetEventSlim();
            private readonly ManualResetEventSlim _continue = new ManualResetEventSlim();
            private readonly SimpleGCodeGenerator _inner = new SimpleGCodeGenerator();

            public bool WaitUntilStarted(TimeSpan timeout) => _started.Wait(timeout);
            public void Continue() => _continue.Set();

            public GCodeProgram Generate(
                IList<OperationBase> operations,
                GCodeSettings settings,
                IProgress<int> progress = null)
                => _inner.Generate(operations, settings, progress);

            /// <summary>
            /// Окно строит траекторию, а программу из неё делает постпроцессор,
            /// поэтому останавливаться нужно здесь.
            /// </summary>
            public GCodeGenerator.Toolpath.ToolPath BuildToolPath(
                IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
            {
                _started.Set();
                if (!_continue.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Test generator was not released.");
                return _inner.BuildToolPath(operations, settings, progress);
            }

            public void Dispose()
            {
                _continue.Set();
                _started.Dispose();
                _continue.Dispose();
            }
        }

        [TestMethod]
        public async Task OperationContentChange_InvalidatesGeneratedProgram()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var operation = OperationFixtures.DrillPoints();
            main.AllOperations.Add(operation);
            await Generate(main);
            AssertGeneratedResultIsAvailable(main);

            operation.NotifyContentChanged();

            AssertGeneratedResultIsUnavailable(main);
        }

        [TestMethod]
        public async Task CollectionMutation_InvalidatesGeneratedProgram()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(OperationFixtures.DrillPoints());
            await Generate(main);
            AssertGeneratedResultIsAvailable(main);

            main.AllOperations.Add(OperationFixtures.ProfileCircle());

            AssertGeneratedResultIsUnavailable(main);
        }

        [TestMethod]
        public async Task AppliedSettingsChange_InvalidatesGeneratedProgram()
        {
            var (main, _, _, settingsStore) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(OperationFixtures.DrillPoints());
            await Generate(main);
            AssertGeneratedResultIsAvailable(main);

            settingsStore.Current.Format.UseComments = !settingsStore.Current.Format.UseComments;
            settingsStore.Save();

            AssertGeneratedResultIsUnavailable(main);
        }

        [TestMethod]
        public async Task ProjectChangedDuringBackgroundGeneration_DiscardsCompletedResult()
        {
            using var generator = new PausingGenerator();
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(generator);
            var operation = OperationFixtures.DrillPoints();
            main.AllOperations.Add(operation);

            var generationTask = ((IAsyncRelayCommand)main.GenerateGCodeCommand).ExecuteAsync(null);
            Assert.IsTrue(generator.WaitUntilStarted(TimeSpan.FromSeconds(2)), "Фоновая генерация не запустилась.");

            operation.Name = "Changed while generating";
            generator.Continue();
            await generationTask;

            Assert.IsFalse(main.IsGenerating);
            Assert.AreEqual(0, main.ProgressPercent);
            AssertGeneratedResultIsUnavailable(main);
        }

        private static Task Generate(GCodeGenerator.ViewModels.MainViewModel main)
        {
            return ((IAsyncRelayCommand)main.GenerateGCodeCommand).ExecuteAsync(null);
        }

        private static void AssertGeneratedResultIsAvailable(GCodeGenerator.ViewModels.MainViewModel main)
        {
            Assert.IsFalse(string.IsNullOrEmpty(main.GCodePreview));
            Assert.IsTrue(main.SaveGCodeCommand.CanExecute(null));
            Assert.IsTrue(main.PreviewGCodeCommand.CanExecute(null));
        }

        private static void AssertGeneratedResultIsUnavailable(GCodeGenerator.ViewModels.MainViewModel main)
        {
            Assert.AreEqual(string.Empty, main.GCodePreview);
            Assert.IsFalse(main.SaveGCodeCommand.CanExecute(null));
            Assert.IsFalse(main.PreviewGCodeCommand.CanExecute(null));
        }
    }
}
