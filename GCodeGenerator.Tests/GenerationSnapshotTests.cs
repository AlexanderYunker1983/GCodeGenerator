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
    /// Слепок документа для генерации: пока программа собирается в фоне,
    /// окно остаётся живым, и пользователь может править операции и настройки.
    /// Генератор обязан видеть состояние на момент запуска, а не смесь старого
    /// и нового.
    /// </summary>
    [TestClass]
    public class GenerationSnapshotTests
    {
        private static DrillPointsOperation Drill(double x)
            => new DrillPointsOperation
            {
                Name = "Drill",
                Holes = { new DrillHole { X = x, Y = 0, Z = 0, TotalDepth = 2, StepDepth = 1 } }
            };

        [TestMethod]
        public void Capture_CopiesOperations_LaterEditsDoNotAffectSnapshot()
        {
            var operation = Drill(10);
            var snapshot = GenerationSnapshot.Capture(new List<OperationBase> { operation }, new GCodeSettings());

            operation.Holes[0].X = 999;
            operation.IsEnabled = false;
            operation.Name = "изменено";

            var copy = (DrillPointsOperation)snapshot.Operations[0];
            Assert.AreEqual(10, copy.Holes[0].X, "Координата отверстия остаётся на момент снимка");
            Assert.IsTrue(copy.IsEnabled, "Признак «включена» остаётся на момент снимка");
            Assert.AreEqual("Drill", copy.Name, "Имя остаётся на момент снимка");
        }

        [TestMethod]
        public void Capture_CopiesSettings_LaterEditsDoNotAffectSnapshot()
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = true;
            settings.Spindle.SpindleSpeedRpm = 12000;
            settings.WorkCoordinate.WorkCoordinateSystem = "G54";

            var snapshot = GenerationSnapshot.Capture(new List<OperationBase>(), settings);

            settings.Format.UseLineNumbers = false;
            settings.Spindle.SpindleSpeedRpm = 1;
            settings.WorkCoordinate.WorkCoordinateSystem = "G59";

            Assert.IsTrue(snapshot.Settings.Format.UseLineNumbers, "Формат остаётся на момент снимка");
            Assert.AreEqual(12000, snapshot.Settings.Spindle.SpindleSpeedRpm, "Шпиндель остаётся на момент снимка");
            Assert.AreEqual("G54", snapshot.Settings.WorkCoordinate.WorkCoordinateSystem,
                "Система координат остаётся на момент снимка");
        }

        [TestMethod]
        public void Capture_KeepsOrderAndCount()
        {
            var operations = new List<OperationBase> { Drill(1), Drill(2), Drill(3) };

            var snapshot = GenerationSnapshot.Capture(operations, new GCodeSettings());

            Assert.AreEqual(3, snapshot.Operations.Count, "Порядок обработки — часть программы");
            for (int i = 0; i < operations.Count; i++)
            {
                var original = (DrillPointsOperation)operations[i];
                var copy = (DrillPointsOperation)snapshot.Operations[i];
                Assert.AreEqual(original.Holes[0].X, copy.Holes[0].X, $"Операция [{i}] на своём месте");
                Assert.AreNotSame(original, copy, $"Операция [{i}] — копия, а не та же ссылка");
            }
        }

        [TestMethod]
        public void Capture_ProducesSameProgramAsOriginals()
        {
            var operations = new List<OperationBase> { Drill(10), Drill(20) };
            var settings = new GCodeSettings();

            var direct = new SimpleGCodeGenerator().Generate(operations, settings);
            var snapshot = GenerationSnapshot.Capture(operations, settings);
            var fromSnapshot = new SimpleGCodeGenerator().Generate(
                new List<OperationBase>(snapshot.Operations), snapshot.Settings);

            CollectionAssert.AreEqual(
                new List<string>(direct.Lines),
                new List<string>(fromSnapshot.Lines),
                "Снимок не меняет вывод генератора");
        }

        /// <summary>
        /// Главная проверка: правка документа, сделанная уже после запуска
        /// генерации, не должна попасть в собираемую программу.
        /// </summary>
        [TestMethod]
        public async Task EditsDuringGeneration_DoNotReachTheGenerator()
        {
            var generator = new BlockingGenerator();
            var (main, _, _, settingsStore) = MainViewModelOperationEditTests.CreateMain(generator);
            settingsStore.Current.Format.UseLineNumbers = true;

            var operation = Drill(10);
            main.OperationsWorkspace.AllOperations.Add(operation);

            var task = ((IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);
            Assert.IsTrue(generator.Started.Wait(TimeSpan.FromSeconds(5)), "Генерация должна начаться");

            // Документ меняется, пока фоновая генерация уже идёт.
            operation.Holes[0].X = 999;
            operation.IsEnabled = false;
            settingsStore.Current.Format.UseLineNumbers = false;

            generator.Continue.Set();
            await task;

            Assert.AreEqual(10, generator.ObservedX, "Генератор видит координату на момент запуска");
            Assert.IsTrue(generator.ObservedEnabled, "Генератор видит признак «включена» на момент запуска");
            Assert.IsTrue(generator.ObservedUseLineNumbers, "Генератор видит настройки на момент запуска");
        }

        /// <summary>
        /// Генератор, который останавливается посреди работы: тест успевает
        /// изменить документ ровно тогда, когда генерация уже начата.
        /// </summary>
        private sealed class BlockingGenerator : IGCodeGenerator
        {
            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(false);

            public ManualResetEventSlim Continue { get; } = new ManualResetEventSlim(false);

            public double ObservedX { get; private set; }

            public bool ObservedEnabled { get; private set; }

            public bool ObservedUseLineNumbers { get; private set; }

            public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
                => new SimpleGCodeGenerator().Generate(operations, settings, progress);

            /// <summary>
            /// Окно строит траекторию, поэтому слепок документа виден здесь:
            /// именно с этими данными работает фоновый поток.
            /// </summary>
            public GCodeGenerator.Toolpath.ToolPath BuildToolPath(
                IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
            {
                Started.Set();
                Continue.Wait(TimeSpan.FromSeconds(5));

                var drill = (DrillPointsOperation)operations[0];
                ObservedX = drill.Holes[0].X;
                ObservedEnabled = drill.IsEnabled;
                ObservedUseLineNumbers = settings.Format.UseLineNumbers;

                return new SimpleGCodeGenerator().BuildToolPath(operations, settings, progress);
            }
        }
    }
}
