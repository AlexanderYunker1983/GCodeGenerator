#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Порядок обработки спиральных и концентрических карманов.</summary>
    [TestClass]
    public sealed class PocketProcessingDirectionTests
    {
        private const double WorkingZ = -1.0;

        [TestMethod]
        public void StrategyDefaults_PreserveHistoricalDirections()
        {
            var operation = Circle(PocketStrategy.Spiral);
            Assert.AreEqual(
                PocketProcessingDirection.CenterOutward,
                operation.ProcessingDirection,
                "прежняя спираль начинается в центре");

            operation.PocketStrategy = PocketStrategy.Concentric;
            Assert.AreEqual(
                PocketProcessingDirection.OutsideIn,
                operation.ProcessingDirection,
                "прежние концентрические проходы начинаются у стенки");

            operation.ProcessingDirection = PocketProcessingDirection.CenterOutward;
            operation.PocketStrategy = PocketStrategy.Spiral;
            operation.PocketStrategy = PocketStrategy.Concentric;
            Assert.AreEqual(
                PocketProcessingDirection.CenterOutward,
                operation.ProcessingDirection,
                "явный выбор сохраняется при смене стратегии");
        }

        [TestMethod]
        public void Spiral_DirectionChangesFirstCutFromCenterToOuterContour()
        {
            var centerOutward = Circle(PocketStrategy.Spiral);
            centerOutward.ProcessingDirection = PocketProcessingDirection.CenterOutward;
            var outsideIn = Circle(PocketStrategy.Spiral);
            outsideIn.ProcessingDirection = PocketProcessingDirection.OutsideIn;

            var centerRadius = FirstWorkingXyRadius(Build(centerOutward));
            var outerRadius = FirstWorkingXyRadius(Build(outsideIn));

            Assert.IsTrue(centerRadius < 0.01, $"первый рез из центра: R={centerRadius:0.###}");
            Assert.IsTrue(outerRadius > 8.9, $"первый рез снаружи: R={outerRadius:0.###}");
        }

        [TestMethod]
        public void Concentric_DirectionReversesContourOrder()
        {
            var centerOutward = Circle(PocketStrategy.Concentric);
            centerOutward.ProcessingDirection = PocketProcessingDirection.CenterOutward;
            var outsideIn = Circle(PocketStrategy.Concentric);
            outsideIn.ProcessingDirection = PocketProcessingDirection.OutsideIn;

            var centerRadius = FirstWorkingXyRadius(Build(centerOutward));
            var outerRadius = FirstWorkingXyRadius(Build(outsideIn));

            Assert.IsTrue(centerRadius < 1.1, $"первый внутренний контур: R={centerRadius:0.###}");
            Assert.IsTrue(outerRadius > 8.9, $"первый внешний контур: R={outerRadius:0.###}");
        }

        [TestMethod]
        public void BothDirections_KeepEveryStrategyOutsideIsland()
        {
            foreach (var strategy in new[] { PocketStrategy.Spiral, PocketStrategy.Concentric })
            {
                foreach (PocketProcessingDirection direction in Enum.GetValues(typeof(PocketProcessingDirection)))
                {
                    var pocket = Rectangle(strategy);
                    pocket.ProcessingDirection = direction;
                    var island = new PocketCircleOperation
                    {
                        PocketMode = PocketMode.Island,
                        Radius = 4,
                    };

                    var segments = WorkingLinearSegments(Build(pocket, island)).ToList();
                    Assert.IsTrue(segments.Count > 0, $"{strategy}, {direction}");
                    foreach (var segment in segments)
                    {
                        Assert.IsTrue(DistanceToSegment(0, 0, segment.from, segment.to) >= 4.99,
                            $"{strategy}, {direction}: рабочий ход пересёк остров");
                    }
                }
            }
        }

        [TestMethod]
        public void OutsideIn_WithIsland_StartsAtExternalBoundary()
        {
            foreach (var strategy in new[] { PocketStrategy.Spiral, PocketStrategy.Concentric })
            {
                var pocket = Rectangle(strategy);
                pocket.ProcessingDirection = PocketProcessingDirection.OutsideIn;
                var island = new PocketCircleOperation
                {
                    PocketMode = PocketMode.Island,
                    Radius = 4,
                };

                var firstRadius = FirstWorkingXyRadius(Build(pocket, island));

                Assert.IsTrue(firstRadius > 15,
                    $"{strategy}: первым должен идти внешний контур, R={firstRadius:0.###}");
            }
        }

        [TestMethod]
        public void Island_DirectionOrdersCompletePathBetweenInnerAndOuterBoundaries()
        {
            foreach (var strategy in new[] { PocketStrategy.Spiral, PocketStrategy.Concentric })
            {
                var centerOutward = Rectangle(strategy);
                centerOutward.ProcessingDirection = PocketProcessingDirection.CenterOutward;
                var outsideIn = Rectangle(strategy);
                outsideIn.ProcessingDirection = PocketProcessingDirection.OutsideIn;
                var island = new PocketCircleOperation
                {
                    PocketMode = PocketMode.Island,
                    Radius = 4,
                };

                var centerPath = Build(centerOutward, island);
                Assert.IsTrue(FirstWorkingXyRadius(centerPath) < 5.1, $"{strategy}: начало у острова");
                Assert.IsTrue(LastWorkingXyRadius(centerPath) > 15, $"{strategy}: окончание снаружи");

                var outsidePath = Build(outsideIn, island);
                Assert.IsTrue(FirstWorkingXyRadius(outsidePath) > 15, $"{strategy}: начало снаружи");
                Assert.IsTrue(LastWorkingXyRadius(outsidePath) < 5.1, $"{strategy}: окончание у острова");
            }
        }

        [TestMethod]
        public void OutsideInSpiral_WorksForEveryPocketGeometry()
        {
            PocketOperationBase[] operations =
            {
                Rectangle(PocketStrategy.Spiral),
                Circle(PocketStrategy.Spiral),
                Prepare(OperationFixtures.PocketEllipse()),
                Prepare(OperationFixtures.PocketDxf()),
            };

            foreach (var operation in operations)
            {
                operation.PocketStrategy = PocketStrategy.Spiral;
                operation.ProcessingDirection = PocketProcessingDirection.OutsideIn;
                Assert.IsTrue(Build(operation).Moves().Any(), operation.GetType().Name);
            }
        }

        [TestMethod]
        public void Project_RoundTripStoresExplicitDirection_AndOmitsStrategyDefault()
        {
            var service = new ProjectFileService();
            var implicitDirection = Circle(PocketStrategy.Spiral);
            var implicitJson = service.Serialize(new OperationBase[] { implicitDirection }, null);
            Assert.IsFalse(implicitJson.Contains("\"ProcessingDirection\"", StringComparison.Ordinal),
                "невыбранное значение не меняет формат старого проекта");

            var explicitDirection = Circle(PocketStrategy.Spiral);
            explicitDirection.ProcessingDirection = PocketProcessingDirection.OutsideIn;
            var json = service.Serialize(new OperationBase[] { explicitDirection }, null);
            Assert.IsTrue(json.Contains("\"ProcessingDirection\":1", StringComparison.Ordinal));

            var loaded = (PocketCircleOperation)service.Deserialize(json).Operations![0];
            Assert.AreEqual(PocketProcessingDirection.OutsideIn, loaded.ProcessingDirection);
            Assert.AreEqual(
                PocketProcessingDirection.OutsideIn,
                loaded.ProcessingDirectionSetting,
                "явное значение остаётся явным после чтения");
        }

        private static PocketCircleOperation Circle(PocketStrategy strategy)
            => Prepare(new PocketCircleOperation { Radius = 10, PocketStrategy = strategy });

        private static PocketRectangleOperation Rectangle(PocketStrategy strategy)
            => Prepare(new PocketRectangleOperation
            {
                Width = 40,
                Height = 30,
                PocketStrategy = strategy,
            });

        private static T Prepare<T>(T operation) where T : PocketOperationBase
        {
            operation.ToolDiameter = 2;
            operation.TotalDepth = 1;
            operation.StepDepth = 1;
            operation.StepPercentOfTool = 100;
            operation.SafeZHeight = 5;
            return operation;
        }

        private static ToolPath Build(params OperationBase[] operations)
            => new SimpleGCodeGenerator().BuildToolPath(operations, new GCodeSettings());

        private static double FirstWorkingXyRadius(ToolPath path)
        {
            var position = (x: 0.0, y: 0.0, z: 0.0);
            foreach (var move in path.Moves())
            {
                var target = (
                    x: move.X ?? position.x,
                    y: move.Y ?? position.y,
                    z: move.Z ?? position.z);
                if (move.Kind == ToolMoveKind.Linear
                    && Math.Abs(target.z - WorkingZ) < 1e-7
                    && (move.X.HasValue || move.Y.HasValue))
                {
                    return Math.Sqrt(target.x * target.x + target.y * target.y);
                }
                position = target;
            }
            Assert.Fail("Не найдено рабочего перемещения XY");
            return 0;
        }

        private static double LastWorkingXyRadius(ToolPath path)
        {
            var position = (x: 0.0, y: 0.0, z: 0.0);
            double? radius = null;
            foreach (var move in path.Moves())
            {
                var target = (
                    x: move.X ?? position.x,
                    y: move.Y ?? position.y,
                    z: move.Z ?? position.z);
                if (move.Kind == ToolMoveKind.Linear
                    && Math.Abs(target.z - WorkingZ) < 1e-7
                    && (move.X.HasValue || move.Y.HasValue))
                {
                    radius = Math.Sqrt(target.x * target.x + target.y * target.y);
                }
                position = target;
            }
            Assert.IsTrue(radius.HasValue, "Не найдено рабочего перемещения XY");
            return radius.GetValueOrDefault();
        }

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
    }
}
