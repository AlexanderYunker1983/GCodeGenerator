using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Детерминированные property-проверки полного конвейера. В отличие от
    /// примеров и golden-файлов они перебирают сотни допустимых сочетаний,
    /// но каждый сбой воспроизводится по seed и номеру случая.
    /// </summary>
    [TestClass]
    public class GenerativeInvariantTests
    {
        private const int CasesPerSeed = 25;

        [TestMethod]
        [DataRow(1103)]
        [DataRow(7919)]
        [DataRow(17027)]
        [DataRow(65537)]
        [DataRow(104729)]
        [DataRow(214748)]
        [DataRow(999983)]
        [DataRow(20260829)]
        public void RandomValidDrillProjects_PreservePipelineInvariants(int seed)
        {
            var random = new Random(seed);
            var generator = new SimpleGCodeGenerator();
            var projects = new ProjectFileService();

            for (var caseIndex = 0; caseIndex < CasesPerSeed; caseIndex++)
            {
                var operations = Operations(random);
                var settings = Settings(random);
                var context = $"seed={seed}, case={caseIndex}";

                var firstPath = generator.BuildToolPath(operations, settings);
                var firstProgram = generator.Generate(operations, settings);
                var secondProgram = generator.Generate(operations, settings);

                CollectionAssert.AreEqual(
                    firstProgram.Lines.ToArray(),
                    secondProgram.Lines.ToArray(),
                    context + ": одинаковый снимок обязан давать одинаковую программу");
                AssertFinite(firstPath, firstProgram, context);

                var json = projects.Serialize(operations, settings);
                var loaded = projects.Deserialize(json);
                Assert.IsNotNull(loaded.Operations, context);
                var loadedSettings = new GCodeSettings
                {
                    Format = loaded.Format!,
                    Spindle = loaded.Spindle!,
                    Coolant = loaded.Coolant!,
                    WorkCoordinate = loaded.WorkCoordinate!,
                };
                var roundTripProgram = generator.Generate(loaded.Operations!, loadedSettings);

                CollectionAssert.AreEqual(
                    firstProgram.Lines.ToArray(),
                    roundTripProgram.Lines.ToArray(),
                    context + ": сохранение и загрузка не должны менять семантику G-code");
                Assert.AreEqual(
                    json,
                    projects.Serialize(loaded.Operations!, loadedSettings),
                    context + ": текущий формат проекта должен быть каноничным после round-trip");
            }
        }

        private static List<OperationBase> Operations(Random random)
        {
            var result = new List<OperationBase>();
            var operationCount = random.Next(1, 5);
            for (var operationIndex = 0; operationIndex < operationCount; operationIndex++)
            {
                var operation = new DrillPointsOperation
                {
                    Name = $"Generated {operationIndex}",
                    Decimals = random.Next(0, OperationValidation.MaxDecimals + 1),
                    FeedXYRapid = Between(random, 100, 5000),
                    FeedXYWork = Between(random, 10, 1000),
                    FeedZRapid = Between(random, 100, 3000),
                    FeedZWork = Between(random, 10, 700),
                };

                var highestZ = double.NegativeInfinity;
                var holeCount = random.Next(1, 10);
                for (var holeIndex = 0; holeIndex < holeCount; holeIndex++)
                {
                    var z = Between(random, -5, 5);
                    var depth = Between(random, 0.1, 12);
                    operation.Holes.Add(new DrillHole
                    {
                        X = Between(random, -250, 250),
                        Y = Between(random, -250, 250),
                        Z = z,
                        TotalDepth = depth,
                        StepDepth = Between(random, 0.05, depth + 1),
                        RetractHeight = z + Between(random, 0, 3),
                        FeedZRapid = Between(random, 100, 3000),
                        FeedZWork = Between(random, 10, 700),
                    });
                    highestZ = Math.Max(highestZ, z);
                }

                operation.SafeZBetweenHoles = highestZ + Between(random, 0.05, 8);
                result.Add(operation);
            }

            return result;
        }

        private static GCodeSettings Settings(Random random)
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = random.Next(2) == 0;
            settings.Format.LineNumberStart = random.Next(0, int.MaxValue);
            settings.Format.LineNumberStep = random.Next(1, 1000000);
            settings.Format.UseComments = random.Next(2) == 0;
            settings.Format.UsePaddedGCodes = random.Next(2) == 0;
            settings.Format.PostProcessorName = random.Next(2) == 0 ? "Generic" : "Grbl";

            settings.Spindle.SpindleSpeedRpm = random.Next(1, GCodeSettingsValidation.MaxSpindleSpeedRpm + 1);
            settings.Spindle.SpindleDelayEnabled = random.Next(2) == 0;
            settings.Spindle.SpindleDelaySeconds = Between(random, 0.01, 10);
            settings.Spindle.SpindleStartCommand = random.Next(2) == 0 ? "M3" : "M4";
            settings.Coolant.CoolantStartEnabled = random.Next(2) == 0;
            settings.Coolant.CoolantStopEnabled = random.Next(2) == 0;

            settings.WorkCoordinate.SetWorkCoordinateSystem = random.Next(2) == 0;
            settings.WorkCoordinate.WorkCoordinateSystem = "G" + random.Next(54, 60);
            settings.WorkCoordinate.AddStartPosition = random.Next(2) == 0;
            settings.WorkCoordinate.StartX = Between(random, -500, 500);
            settings.WorkCoordinate.StartY = Between(random, -500, 500);
            settings.WorkCoordinate.StartZ = Between(random, -50, 50);
            settings.WorkCoordinate.AddEndPosition = random.Next(2) == 0;
            settings.WorkCoordinate.EndX = Between(random, -500, 500);
            settings.WorkCoordinate.EndY = Between(random, -500, 500);
            settings.WorkCoordinate.EndZ = Between(random, -50, 50);
            return settings;
        }

        private static void AssertFinite(
            Toolpath.ToolPath path,
            GCodeProgram program,
            string context)
        {
            foreach (var move in path.Moves())
            {
                Assert.IsTrue(!move.X.HasValue || double.IsFinite(move.X.Value), context + ": X");
                Assert.IsTrue(!move.Y.HasValue || double.IsFinite(move.Y.Value), context + ": Y");
                Assert.IsTrue(!move.Z.HasValue || double.IsFinite(move.Z.Value), context + ": Z");
                Assert.IsTrue(!move.Feed.HasValue || double.IsFinite(move.Feed.Value), context + ": F");
            }

            foreach (var block in program.Blocks)
            {
                foreach (var word in block.Words.Where(word => word.Text == null))
                    Assert.IsTrue(double.IsFinite(word.Number), context + $": word {word.Letter}");
            }

            Assert.IsFalse(program.Lines.Any(line =>
                    line.Contains("NaN", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Infinity", StringComparison.OrdinalIgnoreCase)),
                context + ": текст не должен содержать нечисловые координаты");
        }

        private static double Between(Random random, double min, double max)
            => min + (max - min) * random.NextDouble();
    }
}
