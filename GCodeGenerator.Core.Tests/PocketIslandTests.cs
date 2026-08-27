#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Острова участвуют в геометрии всего проекта: сами не создают путь,
    /// но исключают свою область с запасом на радиус фрезы из любого кармана.
    /// </summary>
    [TestClass]
    public sealed class PocketIslandTests
    {
        private const double WorkingZ = -1.0;

        [TestMethod]
        public void Island_DoesNotCreateOwnToolPathOperation()
        {
            var pocket = RectanglePocket(PocketStrategy.Lines);
            var island = CircleIsland();

            var path = Build(pocket, island);

            Assert.AreEqual(1, path.Operations.Count);
            Assert.AreSame(pocket, path.Operations[0].Source);
            Assert.IsTrue(path.Moves().Any());
        }

        [TestMethod]
        public void EveryStrategy_KeepsToolCenterOutsideIsland()
        {
            foreach (PocketStrategy strategy in Enum.GetValues(typeof(PocketStrategy)))
            {
                var pocket = RectanglePocket(strategy);
                var island = CircleIsland();
                var cuttingSegments = WorkingLinearSegments(Build(pocket, island)).ToList();

                Assert.IsTrue(cuttingSegments.Count > 0, $"{strategy}: есть рабочие перемещения");
                foreach (var segment in cuttingSegments)
                {
                    // Физический радиус острова 4 мм, радиус фрезы 1 мм.
                    // Окружность аппроксимирована хордами с шагом 0,5 мм,
                    // поэтому допускается только их микроскопическая стрела.
                    var distance = DistanceToSegment(0, 0, segment.from, segment.to);
                    Assert.IsTrue(distance >= 4.99,
                        $"{strategy}: рабочий сегмент вошёл в остров, расстояние {distance:0.###}");
                }
            }
        }

        [TestMethod]
        public void EveryPocketGeometry_CanActAsIsland()
        {
            PocketOperationBase[] islands =
            {
                CircleIsland(),
                new PocketRectangleOperation
                {
                    PocketMode = PocketMode.Island,
                    Width = 8,
                    Height = 8,
                },
                new PocketEllipseOperation
                {
                    PocketMode = PocketMode.Island,
                    RadiusX = 5,
                    RadiusY = 3,
                },
                new PocketDxfOperation
                {
                    PocketMode = PocketMode.Island,
                    ClosedContours = new List<Polyline2D> { Square(-4, -4, 4, 4) },
                },
            };

            foreach (var island in islands)
            {
                var path = Build(RectanglePocket(PocketStrategy.Lines), island);
                Assert.AreEqual(1, path.Operations.Count, island.GetType().Name);
                Assert.IsTrue(path.Moves().Any(), island.GetType().Name);
            }
        }

        [TestMethod]
        public void EveryPocketGeometry_RespectsIsland()
        {
            PocketOperationBase[] pockets =
            {
                RectanglePocket(PocketStrategy.Lines),
                Prepare(new PocketCircleOperation { Radius = 20 }),
                Prepare(new PocketEllipseOperation { RadiusX = 20, RadiusY = 15 }),
                Prepare(new PocketDxfOperation
                {
                    ClosedContours = new List<Polyline2D> { Square(-20, -15, 20, 15) },
                }),
            };

            foreach (var pocket in pockets)
            {
                var segments = WorkingLinearSegments(Build(pocket, CircleIsland())).ToList();
                Assert.IsTrue(segments.Count > 0, pocket.GetType().Name);
                foreach (var segment in segments)
                {
                    Assert.IsTrue(DistanceToSegment(0, 0, segment.from, segment.to) >= 4.99,
                        pocket.GetType().Name);
                }
            }
        }

        [TestMethod]
        public void Island_AffectsEveryMachiningPocketRegardlessOfListOrder()
        {
            var first = RectanglePocket(PocketStrategy.Lines);
            first.ReferencePointX = -30;
            var second = RectanglePocket(PocketStrategy.Lines);
            second.ReferencePointX = 30;
            var firstIsland = CircleIsland();
            firstIsland.CenterX = -30;
            var secondIsland = CircleIsland();
            secondIsland.CenterX = 30;

            var path = Build(firstIsland, first, second, secondIsland);

            Assert.AreEqual(2, path.Operations.Count);
            Assert.AreSame(first, path.Operations[0].Source);
            Assert.AreSame(second, path.Operations[1].Source);
        }

        [TestMethod]
        public void IslandCoveringWholePocket_LeavesNoToolMovement()
        {
            var pocket = RectanglePocket(PocketStrategy.Spiral);
            pocket.Width = 10;
            pocket.Height = 10;
            var island = new PocketRectangleOperation
            {
                PocketMode = PocketMode.Island,
                Width = 20,
                Height = 20,
            };

            var path = Build(pocket, island);

            Assert.AreEqual(1, path.Operations.Count, "обычная операция остаётся в структуре программы");
            Assert.AreEqual(0, path.Moves().Count(), "после вычитания обрабатывать нечего");
        }

        [TestMethod]
        public void DxfPocket_UsesEveryContourOfDxfIsland()
        {
            var pocket = new PocketDxfOperation
            {
                ClosedContours = new List<Polyline2D> { Square(-25, -15, 25, 15) },
                ToolDiameter = 2,
                TotalDepth = 1,
                StepDepth = 1,
                StepPercentOfTool = 100,
                PocketStrategy = PocketStrategy.Lines,
                LineAngleDeg = 0,
            };
            var island = new PocketDxfOperation
            {
                PocketMode = PocketMode.Island,
                ClosedContours = new List<Polyline2D>
                {
                    Square(-10, -4, -4, 4),
                    Square(4, -4, 10, 4),
                },
            };

            var segments = WorkingLinearSegments(Build(pocket, island)).ToList();
            Assert.IsTrue(segments.Count > 0);
            foreach (var segment in segments)
            {
                Assert.IsFalse(SegmentEntersBox(segment, -11, -5, -3, 5), "первый DXF-контур");
                Assert.IsFalse(SegmentEntersBox(segment, 3, -5, 11, 5), "второй DXF-контур");
            }
        }

        [TestMethod]
        public void DisabledOrUnrelatedIsland_DoesNotChangeExistingProgram()
        {
            var baselinePocket = RectanglePocket(PocketStrategy.Spiral);
            var baseline = new SimpleGCodeGenerator().Generate(
                new OperationBase?[] { baselinePocket }, new GCodeSettings()).Lines;

            var disabled = CircleIsland();
            disabled.IsEnabled = false;
            var withDisabled = new SimpleGCodeGenerator().Generate(
                new OperationBase?[] { RectanglePocket(PocketStrategy.Spiral), disabled },
                new GCodeSettings()).Lines;

            var unrelated = CircleIsland();
            unrelated.CenterX = 1000;
            var withUnrelated = new SimpleGCodeGenerator().Generate(
                new OperationBase?[] { RectanglePocket(PocketStrategy.Spiral), unrelated },
                new GCodeSettings()).Lines;

            CollectionAssert.AreEqual(baseline.ToList(), withDisabled.ToList());
            CollectionAssert.AreEqual(baseline.ToList(), withUnrelated.ToList());
        }

        [TestMethod]
        public void Island_ValidatesGeometryButIgnoresUnusedCuttingParameters()
        {
            var island = CircleIsland();
            island.ToolDiameter = 0;
            island.TotalDepth = 0;
            island.StepDepth = 0;
            island.FeedXYWork = 0;
            island.Radius = 0;

            var issues = island.Validate();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(nameof(island.Radius), issues[0].Property);
        }

        [TestMethod]
        public void HelicalEntry_MustFitBetweenPocketAndIsland()
        {
            var pocket = RectanglePocket(PocketStrategy.Lines);
            pocket.Width = 20;
            pocket.Height = 20;
            pocket.EntryMode = PocketEntryMode.Helical;
            pocket.HelicalEntryDiameter = 2;
            var island = new PocketRectangleOperation
            {
                PocketMode = PocketMode.Island,
                Width = 14,
                Height = 14,
            };

            var failure = Assert.ThrowsExactly<CoreException>(() => Build(pocket, island));

            Assert.AreEqual(CoreErrorCodes.HelicalEntryDoesNotFit, failure.Code);
        }

        private static PocketRectangleOperation RectanglePocket(PocketStrategy strategy)
            => new PocketRectangleOperation
            {
                Width = 40,
                Height = 30,
                ToolDiameter = 2,
                TotalDepth = 1,
                StepDepth = 1,
                StepPercentOfTool = 100,
                PocketStrategy = strategy,
                LineAngleDeg = 0,
            };

        private static T Prepare<T>(T pocket) where T : PocketOperationBase
        {
            pocket.ToolDiameter = 2;
            pocket.TotalDepth = 1;
            pocket.StepDepth = 1;
            pocket.StepPercentOfTool = 100;
            pocket.PocketStrategy = PocketStrategy.Lines;
            pocket.LineAngleDeg = 0;
            return pocket;
        }

        private static PocketCircleOperation CircleIsland()
            => new PocketCircleOperation
            {
                PocketMode = PocketMode.Island,
                Radius = 4,
            };

        private static Polyline2D Square(double left, double bottom, double right, double top)
            => new Polyline2D
            {
                Points = new List<Point2D>
                {
                    new Point2D { X = left, Y = bottom },
                    new Point2D { X = right, Y = bottom },
                    new Point2D { X = right, Y = top },
                    new Point2D { X = left, Y = top },
                    new Point2D { X = left, Y = bottom },
                },
            };

        private static ToolPath Build(params OperationBase[] operations)
            => new SimpleGCodeGenerator().BuildToolPath(operations, new GCodeSettings());

        private static IEnumerable<((double x, double y) from, (double x, double y) to)>
            WorkingLinearSegments(ToolPath path)
        {
            var position = (x: 0.0, y: 0.0, z: 0.0);
            foreach (var move in path.Moves())
            {
                var target = (
                    x: move.X ?? position.x,
                    y: move.Y ?? position.y,
                    z: move.Z ?? position.z);
                if (move.Kind == ToolMoveKind.Linear
                    && Math.Abs(position.z - WorkingZ) < 1e-7
                    && Math.Abs(target.z - WorkingZ) < 1e-7
                    && (Math.Abs(position.x - target.x) > 1e-7
                        || Math.Abs(position.y - target.y) > 1e-7))
                {
                    yield return ((position.x, position.y), (target.x, target.y));
                }
                position = target;
            }
        }

        private static double DistanceToSegment(
            double x,
            double y,
            (double x, double y) first,
            (double x, double y) second)
        {
            var dx = second.x - first.x;
            var dy = second.y - first.y;
            var lengthSquared = dx * dx + dy * dy;
            if (lengthSquared <= 1e-14)
                return Math.Sqrt(Math.Pow(x - first.x, 2) + Math.Pow(y - first.y, 2));
            var t = ((x - first.x) * dx + (y - first.y) * dy) / lengthSquared;
            t = Math.Max(0, Math.Min(1, t));
            var px = first.x + t * dx;
            var py = first.y + t * dy;
            return Math.Sqrt(Math.Pow(x - px, 2) + Math.Pow(y - py, 2));
        }

        private static bool SegmentEntersBox(
            ((double x, double y) from, (double x, double y) to) segment,
            double left,
            double bottom,
            double right,
            double top)
        {
            for (var sample = 0; sample <= 100; sample++)
            {
                var t = sample / 100.0;
                var x = segment.from.x + (segment.to.x - segment.from.x) * t;
                var y = segment.from.y + (segment.to.y - segment.from.y) * t;
                if (x > left + 1e-5 && x < right - 1e-5
                    && y > bottom + 1e-5 && y < top - 1e-5)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
