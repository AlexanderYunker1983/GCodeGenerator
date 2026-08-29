#nullable enable
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Регрессии проходов, у которых десятичный шаг нельзя точно представить
    /// числом double. Координата не должна порождать лишний последний слой.
    /// </summary>
    [TestClass]
    public sealed class DepthPassTests
    {
        private const double Tolerance = 1e-12;

        [TestMethod]
        public void Profile_OneMillimeterByTenths_HasExactlyTenPasses()
        {
            var operation = OperationFixtures.ProfileCircle();
            operation.TotalDepth = 1.0;
            operation.StepDepth = 0.1;
            var layers = new List<(double CurrentZ, double NextZ)>();

            new ProfileGenerationHelper().GenerateLayerLoop(
                operation,
                (currentZ, nextZ, _) => layers.Add((currentZ, nextZ)),
                Builder(),
                new GCodeSettings());

            AssertTenExactPasses(layers);
        }

        [TestMethod]
        public void Pocket_OneMillimeterByTenths_HasExactlyTenPasses()
        {
            var operation = OperationFixtures.PocketCircle();
            operation.TotalDepth = 1.0;
            operation.StepDepth = 0.1;
            var layers = new List<(double CurrentZ, double NextZ)>();

            new PocketGenerationHelper().GenerateLayerLoop(
                operation,
                (currentZ, nextZ, _) =>
                {
                    layers.Add((currentZ, nextZ));
                    return true;
                },
                Builder(),
                new GCodeSettings());

            AssertTenExactPasses(layers);
        }

        [TestMethod]
        public void Drill_OneMillimeterByTenths_HasExactlyTenPecks()
        {
            var operation = new DrillPointsOperation { DrillMode = DrillMode.Points };
            operation.Holes.Add(new DrillHole
            {
                X = 1,
                Y = 2,
                Z = 0,
                TotalDepth = 1.0,
                StepDepth = 0.1,
                FeedZRapid = 500,
                FeedZWork = 200,
                RetractHeight = 0.3
            });
            operation.SafeZBetweenHoles = 1;

            var path = new SimpleGCodeGenerator().BuildToolPath(
                new OperationBase[] { operation },
                new GCodeSettings());
            var pecks = path.Moves()
                .Where(move => move.Kind == ToolMoveKind.Linear && move.Z.HasValue)
                .Select(move => move.Z!.Value)
                .ToList();

            Assert.AreEqual(10, pecks.Count, "Должно быть ровно десять рабочих заглублений");
            Assert.AreEqual(-1.0, pecks[^1], Tolerance, "Последний проход достигает глубины точно");
            Assert.AreEqual(1, pecks.Count(z => System.Math.Abs(z + 1.0) <= Tolerance),
                "Конечная глубина не повторяется");
        }

        private static void AssertTenExactPasses(IReadOnlyList<(double CurrentZ, double NextZ)> layers)
        {
            Assert.AreEqual(10, layers.Count, "Должно быть ровно десять слоёв");
            Assert.AreEqual(-1.0, layers[^1].NextZ, Tolerance, "Последний слой достигает глубины точно");
            Assert.AreEqual(1, layers.Count(layer => System.Math.Abs(layer.NextZ + 1.0) <= Tolerance),
                "Конечная глубина не повторяется");
        }

        private static ToolPathBuilder Builder()
            => new ToolPathBuilder(new ToolPathOperation("test", "test", 3));
    }
}
