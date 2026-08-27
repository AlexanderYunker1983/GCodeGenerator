#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using GCodeGenerator.Trajectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Винтовой подвод во всех карманах. Проверяется траектория, а не только
    /// наличие G2/G3: диаметр, угол и конечная глубина должны совпадать с
    /// параметрами операции, иначе красивый на вид вход окажется круче или
    /// шире заданного уже на станке.
    /// </summary>
    [TestClass]
    public class PocketHelicalEntryTests
    {
        private const double Tolerance = 1e-7;

        private static PocketCircleOperation Circle()
        {
            return new PocketCircleOperation
            {
                CenterX = 7,
                CenterY = -3,
                Radius = 10,
                ToolDiameter = 2,
                TotalDepth = 1,
                StepDepth = 1,
                StepPercentOfTool = 100,
                PocketStrategy = PocketStrategy.Radial,
                EntryMode = PocketEntryMode.Helical,
                EntryAngle = 10,
                HelicalEntryDiameter = 4,
                Direction = MillingDirection.CounterClockwise,
            };
        }

        private static ToolPath Build(PocketOperationBase operation, bool allowArcs = true)
        {
            var settings = new GCodeSettings();
            settings.Format.AllowArcs = allowArcs;
            return new SimpleGCodeGenerator().BuildToolPath(
                new OperationBase?[] { operation }, settings);
        }

        /// <summary>
        /// Длина винтовой линии определяется углом: tan(a)=глубина/длина XY.
        /// Радиус каждой дуги — половина заданного диаметра, а последняя дуга
        /// приходит точно на рабочую Z.
        /// </summary>
        [TestMethod]
        public void Helix_UsesConfiguredDiameterAngleAndDepth()
        {
            var operation = Circle();
            var moves = Build(operation).Moves().ToList();

            var position = (x: 0.0, y: 0.0, z: 0.0);
            var horizontalLength = 0.0;
            var helixDepth = 0.0;
            var helixCount = 0;
            double? helixStartZ = null;
            var expectedFeed = Math.Min(
                operation.FeedXYWork / Math.Cos(operation.EntryAngle * Math.PI / 180.0),
                operation.FeedZWork / Math.Sin(operation.EntryAngle * Math.PI / 180.0));

            foreach (var move in moves)
            {
                var target = (
                    x: move.X ?? position.x,
                    y: move.Y ?? position.y,
                    z: move.Z ?? position.z);

                if (move is ArcMove arc && arc.EndZ.HasValue)
                {
                    helixStartZ ??= position.z;
                    helixCount++;
                    var centerX = position.x + arc.ArcCenterOffsetX;
                    var centerY = position.y + arc.ArcCenterOffsetY;
                    var radius = Math.Sqrt(
                        Math.Pow(position.x - centerX, 2) + Math.Pow(position.y - centerY, 2));
                    Assert.AreEqual(operation.HelicalEntryDiameter / 2.0, radius, Tolerance,
                        "радиус винтовой траектории");
                    Assert.AreEqual(expectedFeed, arc.ArcFeed, Tolerance,
                        "подача ограничивает и XY-, и Z-составляющую");
                    Assert.AreEqual(ToolMoveKind.ArcCounterClockwise, arc.Kind,
                        "направление подвода следует направлению фрезерования");

                    var from = Math.Atan2(position.y - centerY, position.x - centerX);
                    var to = Math.Atan2(target.y - centerY, target.x - centerX);
                    var sweep = to - from;
                    while (sweep <= 0) sweep += 2 * Math.PI;
                    horizontalLength += radius * sweep;
                    helixDepth += position.z - target.z;
                }

                position = target;
            }

            Assert.IsTrue(helixCount > 0, "винтовой вход состоит из дуг");
            Assert.AreEqual(operation.ContourHeight + operation.RetractHeight, helixStartZ!.Value, Tolerance,
                "спираль начинается на высоте отвода над верхом слоя");
            Assert.AreEqual(operation.TotalDepth + operation.RetractHeight, helixDepth, Tolerance,
                "спуск включает безопасную высоту над слоем");
            var actualAngle = Math.Atan(helixDepth / horizontalLength) * 180.0 / Math.PI;
            Assert.AreEqual(operation.EntryAngle, actualAngle, Tolerance, "заданный угол подвода");
        }

        /// <summary>
        /// Общий генератор карманов обслуживает четыре геометрии. Новое поле
        /// не должно работать только для простого круга и теряться в DXF или
        /// при смещении эллипса.
        /// </summary>
        [TestMethod]
        public void EveryPocketOperationType_UsesHelicalEntry()
        {
            PocketOperationBase[] operations =
            {
                OperationFixtures.PocketRectangle(),
                OperationFixtures.PocketCircle(),
                OperationFixtures.PocketEllipse(),
                OperationFixtures.PocketDxf(),
            };

            foreach (var operation in operations)
            {
                operation.EntryMode = PocketEntryMode.Helical;
                operation.EntryAngle = 8;
                operation.HelicalEntryDiameter = 1;
                operation.TotalDepth = 1;
                operation.StepDepth = 1;

                var helicalArcs = Build(operation).Moves()
                    .OfType<ArcMove>()
                    .Count(arc => arc.EndZ.HasValue);

                Assert.IsTrue(helicalArcs > 0,
                    $"{operation.GetType().Name}: винтовой подвод построен");
            }
        }

        /// <summary>
        /// AllowArcs=false запрещает любые G2/G3. Геометрия подвода при этом
        /// остаётся той же и разбивается на короткие пространственные G1.
        /// </summary>
        [TestMethod]
        public void ArcsDisabled_HelixIsApproximatedByLinearMoves()
        {
            var operation = Circle();

            var moves = Build(operation, allowArcs: false).Moves().ToList();
            var helicalLines = moves
                .Where(move => move.Kind == ToolMoveKind.Linear
                               && move.X.HasValue
                               && move.Y.HasValue
                               && move.Z.HasValue)
                .ToList();

            Assert.AreEqual(0, moves.OfType<ArcMove>().Count(), "G2/G3 запрещены настройкой");
            Assert.IsTrue(helicalLines.Count >= 4, "винтовая линия разбита на хорды");
            Assert.AreEqual(-operation.TotalDepth, helicalLines.Last().Z!.Value, Tolerance,
                "последняя хорда приходит на глубину слоя");
        }

        [TestMethod]
        public void HelicalEntry_IsWrittenAsArcWithZ()
        {
            var operation = Circle();
            var settings = new GCodeSettings();

            var program = new SimpleGCodeGenerator().Generate(
                new OperationBase?[] { operation }, settings);
            var helicalLine = program.Lines.FirstOrDefault(line =>
                line.Contains("G3 ")
                && line.Contains(" Z")
                && line.Contains(" I")
                && line.Contains(" J"));

            Assert.IsNotNull(helicalLine,
                "винтовой подвод выводится одним кадром дуги с одновременной координатой Z");
        }

        /// <summary>
        /// На последующих слоях безопасное начало считается от верха именно
        /// этого слоя, а не остаётся на поверхности заготовки.
        /// </summary>
        [TestMethod]
        public void EveryLayer_HelixStartsAboveItsCurrentHeight()
        {
            var operation = Circle();
            operation.TotalDepth = 2;
            operation.StepDepth = 1;
            operation.RetractHeight = 0.3;

            var starts = new List<double>();
            var positionZ = 0.0;
            var previousWasHelix = false;
            foreach (var move in Build(operation).Moves())
            {
                var isHelix = move is ArcMove arc && arc.EndZ.HasValue;
                if (isHelix && !previousWasHelix)
                    starts.Add(positionZ);

                positionZ = move.Z ?? positionZ;
                previousWasHelix = isHelix;
            }

            Assert.AreEqual(2, starts.Count, "по одному винтовому входу на слой");
            Assert.AreEqual(0.3, starts[0], Tolerance, "первый слой: 0 + высота отвода");
            Assert.AreEqual(-0.7, starts[1], Tolerance, "второй слой: -1 + высота отвода");
        }

        /// <summary>
        /// Каждая раздельная область DXF — самостоятельный карман и получает
        /// собственный подвод. Один вход на первую область не оставляет
        /// вторую с вертикальным ударом в материал.
        /// </summary>
        [TestMethod]
        public void DxfSeparateAreas_EachGetsItsOwnHelix()
        {
            var operation = new PocketDxfOperation
            {
                ClosedContours = new List<Polyline2D>
                {
                    Square(0, 0, 10),
                    Square(20, 0, 10),
                },
                ToolDiameter = 2,
                TotalDepth = 1,
                StepDepth = 1,
                StepPercentOfTool = 100,
                EntryMode = PocketEntryMode.Helical,
                EntryAngle = 10,
                HelicalEntryDiameter = 2,
            };

            var helixGroups = 0;
            var previousWasHelix = false;
            foreach (var move in Build(operation).Moves())
            {
                var isHelix = move is ArcMove arc && arc.EndZ.HasValue;
                if (isHelix && !previousWasHelix)
                    helixGroups++;
                previousWasHelix = isHelix;
            }

            Assert.AreEqual(2, helixGroups, "по одной винтовой траектории на область");
        }

        /// <summary>
        /// Диаметр относится к центру фрезы, а допустимая область уже
        /// смещена внутрь на её радиус. Слишком широкая спираль должна дать
        /// явный отказ до построения опасной траектории.
        /// </summary>
        [TestMethod]
        public void HelixOutsidePocket_IsRejected()
        {
            var operation = Circle();
            operation.Radius = 5;
            operation.ToolDiameter = 2;
            operation.HelicalEntryDiameter = 9;

            var failure = Assert.Throws<CoreException>(() => Build(operation));

            Assert.AreEqual(CoreErrorCodes.HelicalEntryDoesNotFit, failure.Code);
            StringAssert.Contains(failure.Message, "9");
        }

        /// <summary>
        /// Предпросмотр получает ту же винтовую дугу, что постпроцессор:
        /// промежуточные точки плавно опускаются, а не рисуются плоским кругом.
        /// </summary>
        [TestMethod]
        public void HelicalArc_PreviewInterpolatesZ()
        {
            var scene = ToolPathSceneBuilder.Build(Build(Circle()));
            var helix = scene.Segments.FirstOrDefault(segment =>
                (segment.MoveType == MoveType.ArcCW || segment.MoveType == MoveType.ArcCCW)
                && Math.Abs(segment.End.Z - segment.Start.Z) > Tolerance);

            Assert.IsNotNull(helix, "в сцене есть винтовая дуга");
            Assert.IsNotNull(helix.InterpolatedPoints);
            Assert.IsTrue(helix.InterpolatedPoints.Count >= 4);
            Assert.AreEqual(helix.Start.Z, helix.InterpolatedPoints.First().Z, Tolerance);
            Assert.AreEqual(helix.End.Z, helix.InterpolatedPoints.Last().Z, Tolerance);
            for (int i = 1; i < helix.InterpolatedPoints.Count; i++)
            {
                Assert.IsTrue(
                    helix.InterpolatedPoints[i].Z <= helix.InterpolatedPoints[i - 1].Z + Tolerance,
                    "Z по винтовой дуге убывает монотонно");
            }
        }

        private static Polyline2D Square(double x, double y, double size)
        {
            return new Polyline2D
            {
                Points = new List<Point2D>
                {
                    new Point2D { X = x, Y = y },
                    new Point2D { X = x + size, Y = y },
                    new Point2D { X = x + size, Y = y + size },
                    new Point2D { X = x, Y = y + size },
                    new Point2D { X = x, Y = y },
                }
            };
        }
    }
}
