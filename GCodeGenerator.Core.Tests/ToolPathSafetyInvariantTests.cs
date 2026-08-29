#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Сквозные инварианты готовой траектории. Они проверяют физический
    /// смысл движений независимо от конкретного текста G-code и дополняют
    /// точечные регрессии геометрии.
    /// </summary>
    [TestClass]
    public sealed class ToolPathSafetyInvariantTests
    {
        private const double Tolerance = 1e-6;
        private const double BoundarySlack = 1e-4;

        [TestMethod]
        public void ClosedProfileFixtures_EndEveryCuttingSpanAtItsStart()
        {
            foreach (var operation in ClosedProfiles())
            {
                operation.EntryMode = EntryMode.Vertical;
                operation.TotalDepth = 1;
                operation.StepDepth = 1;
                var path = Build(operation);
                var spans = 0;
                var x = 0.0;
                var y = 0.0;
                var z = 0.0;
                (double X, double Y)? spanStart = null;

                foreach (var move in path.Moves())
                {
                    var targetX = move.X ?? x;
                    var targetY = move.Y ?? y;
                    var targetZ = move.Z ?? z;

                    if (!spanStart.HasValue
                        && move.Kind == ToolMoveKind.Linear
                        && move.Z.HasValue
                        && !move.X.HasValue
                        && !move.Y.HasValue
                        && Math.Abs(targetZ + 1.0) <= Tolerance)
                    {
                        spanStart = (x, y);
                    }
                    else if (spanStart.HasValue && move.Kind == ToolMoveKind.Rapid)
                    {
                        spans++;
                        AssertPoint(spanStart.Value, (x, y),
                            operation.GetType().Name + ": замкнутый режущий участок");
                        spanStart = null;
                    }

                    x = targetX;
                    y = targetY;
                    z = targetZ;
                }

                Assert.IsTrue(spans > 0, operation.GetType().Name + ": не найден режущий участок");
            }
        }

        [TestMethod]
        public void EveryPocketStrategy_KeepsWorkingSegmentsInsideThePocket()
        {
            foreach (var strategy in Enum.GetValues<PocketStrategy>())
            {
                foreach (var operation in Pockets())
                {
                    operation.PocketStrategy = strategy;
                    var geometry = OperationCatalog.CreatePocketGeometry(operation);
                    var containmentRadius = Math.Max(
                        0.0,
                        operation.ToolDiameter / 2.0 - BoundarySlack);
                    var separateAreas = geometry.SplitsIntoAreas
                        ? geometry.GetAreas(containmentRadius, taperOffset: 0)
                        : Array.Empty<GCodeGenerator.GCodeGenerators.Geometry.IPocketGeometry>();
                    var path = Build(operation);
                    var x = 0.0;
                    var y = 0.0;
                    var z = 0.0;
                    var checkedSamples = 0;

                    foreach (var move in path.Moves())
                    {
                        var targetX = move.X ?? x;
                        var targetY = move.Y ?? y;
                        var targetZ = move.Z ?? z;

                        if (move.Kind != ToolMoveKind.Rapid
                            && targetZ < operation.ContourHeight - Tolerance
                            && (move.X.HasValue || move.Y.HasValue))
                        {
                            var distance = Math.Sqrt(
                                Math.Pow(targetX - x, 2) + Math.Pow(targetY - y, 2));
                            var samples = Math.Max(1, (int)Math.Ceiling(distance / 0.25));
                            var taper = GCodeGenerationHelper.CalculateTaperOffset(
                                operation.ContourHeight - targetZ,
                                operation.WallTaperAngleDeg);

                            for (var sample = 0; sample <= samples; sample++)
                            {
                                var t = (double)sample / samples;
                                var sampleX = x + (targetX - x) * t;
                                var sampleY = y + (targetY - y) * t;
                                checkedSamples++;
                                var inside = geometry.SplitsIntoAreas
                                    ? separateAreas.Any(area => area.IsPointInside(
                                        sampleX, sampleY, toolRadius: 0, taperOffset: taper))
                                    : geometry.IsPointInside(
                                        sampleX, sampleY, containmentRadius, taper);
                                Assert.IsTrue(
                                    inside,
                                    FormattableString.Invariant(
                                        $"{operation.GetType().Name}/{strategy}: рабочий ход вышел из кармана в [{sampleX:0.###}, {sampleY:0.###}] на Z={targetZ:0.###}"));
                            }
                        }

                        x = targetX;
                        y = targetY;
                        z = targetZ;
                    }

                    Assert.IsTrue(checkedSamples > 0,
                        operation.GetType().Name + "/" + strategy + ": нет проверенных рабочих ходов");
                }
            }
        }

        [TestMethod]
        public void MillingFixtures_DoNotRapidThroughUncutMaterial()
        {
            var operations = ClosedProfiles().Cast<MillingOperationBase>()
                .Concat(Pockets())
                .ToList();
            operations.Add(OperationFixtures.ProfileCircleAngledEntry());

            foreach (var operation in operations)
            {
                var path = Build(operation);
                var cleared = new List<(double X, double Y, double Z)>();
                var x = 0.0;
                var y = 0.0;
                var z = 0.0;

                foreach (var move in path.Moves())
                {
                    var targetX = move.X ?? x;
                    var targetY = move.Y ?? y;
                    var targetZ = move.Z ?? z;

                    if (move.Kind == ToolMoveKind.Rapid)
                    {
                        var horizontalDistance = Math.Sqrt(
                            Math.Pow(targetX - x, 2) + Math.Pow(targetY - y, 2));
                        Assert.IsFalse(
                            horizontalDistance > Tolerance
                            && z < operation.ContourHeight - Tolerance,
                            FormattableString.Invariant(
                                $"{operation.GetType().Name}: горизонтальный G0 на Z={z:0.###} идёт ниже верха материала"));

                        if (move.Z.HasValue
                            && targetZ < z - Tolerance
                            && targetZ < operation.ContourHeight - Tolerance)
                        {
                            Assert.IsTrue(
                                cleared.Any(point =>
                                    Distance(point.X, point.Y, x, y) <= Tolerance
                                    && point.Z <= targetZ + Tolerance),
                                FormattableString.Invariant(
                                    $"{operation.GetType().Name}: G0 опускается до Z={targetZ:0.###} в ещё не обработанной точке [{x:0.###}, {y:0.###}]"));
                        }
                    }
                    else if (targetZ < operation.ContourHeight - Tolerance)
                    {
                        cleared.Add((targetX, targetY, targetZ));
                    }

                    x = targetX;
                    y = targetY;
                    z = targetZ;
                }
            }
        }

        private static ToolPath Build(OperationBase operation)
            => new SimpleGCodeGenerator().BuildToolPath(
                new OperationBase?[] { operation },
                new GCodeSettings());

        private static IEnumerable<ProfileOperationBase> ClosedProfiles()
        {
            yield return OperationFixtures.ProfileRectangle();
            yield return OperationFixtures.ProfileRoundedRectangle();
            yield return OperationFixtures.ProfileCircle();
            yield return OperationFixtures.ProfileEllipse();
            yield return OperationFixtures.ProfilePolygon();
            yield return OperationFixtures.ProfileDxf();
        }

        private static IEnumerable<PocketOperationBase> Pockets()
        {
            yield return OperationFixtures.PocketRectangle();
            yield return OperationFixtures.PocketCircle();
            yield return OperationFixtures.PocketEllipse();
            yield return OperationFixtures.PocketDxf();
        }

        private static void AssertPoint(
            (double X, double Y) expected,
            (double X, double Y) actual,
            string message)
        {
            Assert.AreEqual(expected.X, actual.X, Tolerance, message + ": X");
            Assert.AreEqual(expected.Y, actual.Y, Tolerance, message + ": Y");
        }

        private static double Distance(double firstX, double firstY, double secondX, double secondY)
            => Math.Sqrt(Math.Pow(secondX - firstX, 2) + Math.Pow(secondY - firstY, 2));
    }
}
