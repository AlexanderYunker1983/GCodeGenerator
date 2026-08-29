using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Траектория инструмента как отдельный слой.
    ///
    /// Раньше генераторы сразу писали G-код, поэтому геометрия существовала
    /// в трёх независимых видах: программа, двумерный предпросмотр, строивший
    /// контуры заново из моделей, и трёхмерный, разбиравший готовую программу
    /// обратно. Теперь операция описывает движение инструмента, а программу
    /// из него делает постпроцессор — и он же единственный знает о станке.
    /// </summary>
    [TestClass]
    public class ToolPathTests
    {
        private static List<OperationBase> TwoOperations()
            => new List<OperationBase>
            {
                OperationFixtures.DrillPoints(),
                OperationFixtures.ProfileCircle()
            };

        [TestMethod]
        public void ToolPath_KeepsOperationsInOrder()
        {
            var toolPath = new SimpleGCodeGenerator().BuildToolPath(TwoOperations(), new GCodeSettings());

            Assert.AreEqual(2, toolPath.Operations.Count, "Порядок обработки — часть траектории");
            StringAssert.Contains(toolPath.Operations[0].Description, "Drill");
            StringAssert.Contains(toolPath.Operations[1].Description, "Circle");
        }

        [TestMethod]
        public void ToolPath_CarriesMovesWithCoordinatesAndFeed()
        {
            var toolPath = new SimpleGCodeGenerator().BuildToolPath(TwoOperations(), new GCodeSettings());
            var moves = toolPath.Moves().ToList();

            Assert.IsTrue(moves.Count > 0, "Траектория не пуста");
            Assert.IsTrue(moves.Any(m => m.Kind == ToolMoveKind.Rapid), "Есть холостые ходы");
            Assert.IsTrue(moves.Any(m => m.Kind == ToolMoveKind.Linear), "Есть рабочие перемещения");
            Assert.IsTrue(moves.All(m => m.Feed == null || m.Feed > 0), "Подача задана положительной");
        }

        /// <summary>
        /// Точность вывода — свойство операции, а не движения: она нужна
        /// постпроцессору и переносится вместе с траекторией.
        /// </summary>
        [TestMethod]
        public void ToolPath_CarriesOperationDecimals()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.Decimals = 5;

            var toolPath = new SimpleGCodeGenerator()
                .BuildToolPath(new List<OperationBase> { operation }, new GCodeSettings());

            Assert.AreEqual(5, toolPath.Operations[0].Decimals);
        }

        [TestMethod]
        public void DisabledOperation_LeavesNoTrace()
        {
            var operations = TwoOperations();
            operations[0].IsEnabled = false;

            var toolPath = new SimpleGCodeGenerator().BuildToolPath(operations, new GCodeSettings());

            Assert.AreEqual(1, toolPath.Operations.Count, "Отключённая операция в траекторию не попадает");
        }

        /// <summary>
        /// Программа собирается из траектории: тот же слепок, поданный
        /// постпроцессору, даёт тот же текст.
        /// </summary>
        [TestMethod]
        public void PostProcessor_BuildsProgramFromToolPath()
        {
            var operations = TwoOperations();
            var settings = new GCodeSettings();
            var generator = new SimpleGCodeGenerator();

            var direct = generator.Generate(operations, settings);
            var fromToolPath = new GenericPostProcessor().Build(generator.BuildToolPath(operations, settings), settings);

            CollectionAssert.AreEqual(
                new List<string>(direct.Lines),
                new List<string>(fromToolPath.Lines),
                "Программа целиком определяется траекторией и настройками");
        }

        /// <summary>
        /// Диалект станка сменяем: в реестр можно добавить свою стойку,
        /// настройка выбирает её по ключу, и ни одна операция об этом не
        /// узнает. Ради этого слой и появился.
        /// </summary>
        [TestMethod]
        public void PostProcessor_IsReplaceable()
        {
            var generator = new SimpleGCodeGenerator(
                new OperationGeneratorRegistry(),
                new PostProcessorRegistry(new IPostProcessor[] { new CountingPostProcessor() }));
            var settings = new GCodeSettings();
            settings.Format.PostProcessorName = "Counting";

            var program = generator.Generate(TwoOperations(), settings);

            Assert.AreEqual(1, program.Lines.Count, "Программу построил заданный постпроцессор");
            StringAssert.StartsWith(program.Lines[0], "moves=");
        }

        /// <summary>
        /// Траектория не содержит ни одного G-слова: она о движении, а не
        /// о языке стойки.
        /// </summary>
        [TestMethod]
        public void ToolPath_HasNoMachineWords()
        {
            var toolPath = new SimpleGCodeGenerator().BuildToolPath(TwoOperations(), new GCodeSettings());

            foreach (var operation in toolPath.Operations)
            {
                foreach (var note in operation.Items.OfType<ToolPathNote>())
                {
                    Assert.IsFalse(note.Text.Contains("G0") || note.Text.Contains("G1"),
                        $"Пояснение говорит о движении, а не о командах: «{note.Text}»");
                }
            }
        }

        [TestMethod]
        public void EmptyProject_GivesEmptyToolPath()
        {
            var toolPath = new SimpleGCodeGenerator().BuildToolPath(new List<OperationBase>(), new GCodeSettings());

            Assert.IsTrue(toolPath.IsEmpty, "Пустой проект — пустая траектория");
        }

        /// <summary>Постпроцессор, считающий перемещения вместо вывода программы.</summary>
        private sealed class CountingPostProcessor : IPostProcessor
        {
            public string Key => "Counting";

            public string Name => "Counting";

            public GCodeProgram Build(
                ToolPath toolPath,
                GCodeSettings settings,
                System.Threading.CancellationToken cancellation = default)
            {
                cancellation.ThrowIfCancellationRequested();
                var program = new GCodeProgram();
                program.Lines.Add($"moves={toolPath.Moves().Count()}");
                return program;
            }
        }
    }
}
