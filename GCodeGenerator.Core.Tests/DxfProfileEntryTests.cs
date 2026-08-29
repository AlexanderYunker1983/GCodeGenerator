using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Точка входа DXF-профиля лежит на смещённой траектории, а не на линии
    /// чертежа. В этой точке выполняются подвод, врезание, витки рампы
    /// и быстрые возвраты между ними: колонку над точкой чертежа никто
    /// не фрезерует, и прежняя «упрощённая версия без смещения» ставила
    /// центр фрезы прямо на кромку детали — врезание зарезало кромку
    /// на радиус инструмента, а быстрый спуск между витками рампы бил
    /// в нетронутый материал.
    /// </summary>
    [TestClass]
    public class DxfProfileEntryTests
    {
        private const double ToolRadius = 1.5;

        /// <summary>Замкнутый квадрат чертежа 20×20 с углом в начале координат.</summary>
        private static ProfileDxfOperation Square(ToolPathMode mode)
        {
            var square = new Polyline2D
            {
                Points =
                {
                    new Point2D { X = 0.0, Y = 0.0 },
                    new Point2D { X = 20.0, Y = 0.0 },
                    new Point2D { X = 20.0, Y = 20.0 },
                    new Point2D { X = 0.0, Y = 20.0 },
                    new Point2D { X = 0.0, Y = 0.0 },
                },
            };

            return new ProfileDxfOperation
            {
                Polylines = new List<Polyline2D> { square },
                ToolPathMode = mode,
                ToolDiameter = ToolRadius * 2.0,
                TotalDepth = 2.0,
                StepDepth = 1.0,
                ContourHeight = 0.0,
                SafeZHeight = 5.0,
                FeedXYRapid = 1000.0,
                FeedXYWork = 300.0,
                FeedZRapid = 500.0,
                FeedZWork = 200.0,
                Decimals = 3,
            };
        }

        /// <summary>
        /// Стартовая точка — вершина смещённого контура, согласованная
        /// с порядком обхода: первый рез начинается в ней.
        /// </summary>
        [TestMethod]
        [DataRow(ToolPathMode.Outside)]
        [DataRow(ToolPathMode.Inside)]
        public void StartPoint_IsVertexOfOffsetContour(ToolPathMode mode)
        {
            var op = Square(mode);
            var geometry = new DxfProfileGeometry(op);

            var start = geometry.GetStartPoint(0.0);
            var contour = geometry.GetOrderedContours(GeometryTolerances.Vertex)[0];
            var expected = op.Direction == MillingDirection.Clockwise
                ? contour[contour.Count - 1]
                : contour[0];

            Assert.AreEqual(expected.x, start.x, 1e-9);
            Assert.AreEqual(expected.y, start.y, 1e-9);
        }

        /// <summary>
        /// Врезание происходит на расстоянии не меньше радиуса фрезы от линии
        /// чертежа — прежде колонка врезания стояла прямо на ней (расстояние
        /// ноль), и кромка зарезалась на радиус инструмента.
        /// </summary>
        [TestMethod]
        [DataRow(ToolPathMode.Outside)]
        [DataRow(ToolPathMode.Inside)]
        public void EntryPlunge_KeepsToolRadiusFromDrawing(ToolPathMode mode)
        {
            var op = Square(mode);
            var drawing = op.Polylines[0].Points;

            var toolPath = OperationToolPath.Build(new UnifiedProfileGenerator(), op, new GCodeSettings());

            var x = 0.0;
            var y = 0.0;
            var z = 0.0;
            var plunges = 0;
            foreach (var move in toolPath.Moves())
            {
                var targetZ = move.Z ?? z;
                if (targetZ < z - 1e-9 && targetZ < op.ContourHeight - 1e-9)
                {
                    var distance = DistanceToDrawing(x, y, drawing);
                    Assert.IsTrue(distance >= ToolRadius - 0.01, FormattableString.Invariant(
                        $"врезание на глубину {targetZ} в точке ({x}; {y}) — в {distance:0.000} от линии чертежа, ближе радиуса фрезы"));
                    plunges++;
                }

                x = move.X ?? x;
                y = move.Y ?? y;
                z = targetZ;
            }

            Assert.IsTrue(plunges > 0, "в программе должно быть хотя бы одно врезание");
        }

        /// <summary>
        /// Замкнутая полилиния хранится без повторной первой вершины, но рез
        /// обязан вернуться из последней вершины в стартовую. Прежде этот
        /// четвёртый участок квадрата отсутствовал на каждом слое.
        /// </summary>
        [TestMethod]
        [DataRow(MillingDirection.CounterClockwise)]
        [DataRow(MillingDirection.Clockwise)]
        public void ClosedPolyline_CutsClosingEdge(MillingDirection direction)
        {
            var op = Square(ToolPathMode.OnLine);
            op.Direction = direction;
            op.TotalDepth = 1.0;
            op.StepDepth = 1.0;

            var moves = OperationToolPath.Build(new UnifiedProfileGenerator(), op, new GCodeSettings())
                .Moves()
                .Where(move => move.Kind == ToolMoveKind.Linear && move.X.HasValue && move.Y.HasValue)
                .ToList();

            Assert.AreEqual(5, moves.Count,
                "Нулевой ход в стартовую вершину и четыре стороны квадрата выводятся ровно один раз");
            Assert.AreEqual(moves[0].X, moves[moves.Count - 1].X);
            Assert.AreEqual(moves[0].Y, moves[moves.Count - 1].Y);
        }

        /// <summary>
        /// Рампа относится к первому контуру. Плоский список точек двух
        /// квадратов создавал между ними вымышленное ребро, и часть спуска
        /// шла через заготовку по диагонали в сто миллиметров до второй
        /// области. Переход ко второму контуру допустим только через SafeZ.
        /// </summary>
        [TestMethod]
        public void AngledEntry_WithDisconnectedContours_StaysOnFirstContour()
        {
            var operation = Square(ToolPathMode.OnLine);
            operation.Polylines.Add(new Polyline2D
            {
                Points =
                {
                    new Point2D { X = 100.0, Y = 0.0 },
                    new Point2D { X = 120.0, Y = 0.0 },
                    new Point2D { X = 120.0, Y = 20.0 },
                    new Point2D { X = 100.0, Y = 20.0 },
                    new Point2D { X = 100.0, Y = 0.0 },
                },
            });
            operation.EntryMode = EntryMode.Angled;
            operation.EntryAngle = 1.0;
            operation.TotalDepth = 1.0;
            operation.StepDepth = 1.0;

            var path = OperationToolPath.Build(
                new UnifiedProfileGenerator(), operation, new GCodeSettings());
            var firstDrawing = operation.Polylines[0].Points;
            var previousZ = 0.0;
            var rampPoints = 0;

            foreach (var move in path.Moves())
            {
                var targetZ = move.Z ?? previousZ;
                if (move.Kind == ToolMoveKind.Linear
                    && move.X.HasValue
                    && move.Y.HasValue
                    && targetZ < previousZ - GeometryTolerances.Degenerate)
                {
                    Assert.IsTrue(
                        DistanceToDrawing(move.X.Value, move.Y.Value, firstDrawing) < 1e-6,
                        $"точка рампы ({move.X.Value:0.###}; {move.Y.Value:0.###}) ушла с первого контура");
                    rampPoints++;
                }

                previousZ = targetZ;
            }

            Assert.IsTrue(rampPoints > 0, "наклонный вход содержит рабочие точки спуска");
        }

        private static double DistanceToDrawing(double x, double y, IReadOnlyList<Point2D> drawing)
        {
            var distance = double.MaxValue;
            for (int i = 0; i < drawing.Count - 1; i++)
            {
                distance = Math.Min(distance, Geometry2D.DistanceToSegment(
                    x, y,
                    drawing[i].X, drawing[i].Y,
                    drawing[i + 1].X, drawing[i + 1].Y,
                    GeometryTolerances.Degenerate));
            }

            return distance;
        }
    }
}
