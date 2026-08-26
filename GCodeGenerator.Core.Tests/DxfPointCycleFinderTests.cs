using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfPointCycleFinderTests
    {
        [TestMethod]
        public void FindContours_SquareGraph_ReturnsOneUniqueClosedCycle()
        {
            var segments = new List<Polyline2D>
            {
                Segment((0, 0), (10, 0)),
                Segment((10, 0), (10, 10)),
                Segment((10, 10), (0, 10)),
                Segment((0, 10), (0, 0)),
            };

            var contours = new DxfPointCycleFinder(0.001).FindContours(segments);

            Assert.AreEqual(1, contours.Count);
            var points = contours[0].Points;
            Assert.AreEqual(5, points.Count);
            AssertPoint(points[0], points[4].X, points[4].Y);
            Assert.IsTrue(ContainsPoint(points, 0, 0));
            Assert.IsTrue(ContainsPoint(points, 10, 0));
            Assert.IsTrue(ContainsPoint(points, 10, 10));
            Assert.IsTrue(ContainsPoint(points, 0, 10));
        }

        /// <summary>
        /// Два цикла через одни и те же вершины разными рёбрами — оба
        /// сохраняются. Квадрат с диагоналями: 4 треугольника и 3 обхода
        /// всех четырёх вершин (по сторонам и через каждую из диагоналей).
        /// Прежний ключ дедупликации строился по множеству вершин, склеивал
        /// три четырёхвершинных цикла в один и молча терял две области.
        /// </summary>
        [TestMethod]
        public void FindContours_SquareWithDiagonals_KeepsCyclesThatShareVertices()
        {
            var segments = new List<Polyline2D>
            {
                Segment((0, 0), (10, 0)),
                Segment((10, 0), (10, 10)),
                Segment((10, 10), (0, 10)),
                Segment((0, 10), (0, 0)),
                Segment((0, 0), (10, 10)),
                Segment((10, 0), (0, 10)),
            };

            var contours = new DxfPointCycleFinder(0.001).FindContours(segments);

            Assert.AreEqual(7, contours.Count,
                "4 треугольника + 3 разных обхода четырёх вершин: вершинный ключ оставлял 5");
        }

        /// <summary>
        /// Комбинаторный взрыв получает честный отказ, а не вечный поиск.
        /// Решётка пересекающихся линий — худший случай: простых путей в ней
        /// экспоненциально много, предел глубины ветвление не ограничивает.
        /// До появления бюджета импорт такого чертежа не завершался никогда,
        /// и отменить его тоже было нельзя.
        /// </summary>
        [TestMethod]
        public void FindContours_DenseGrid_FailsFastInsteadOfSearchingForever()
        {
            var failure = Assert.Throws<CoreException>(
                () => new DxfPointCycleFinder(0.001).FindContours(GridSegments(7)));

            Assert.AreEqual(CoreErrorCodes.DxfTooComplex, failure.Code);
        }

        /// <summary>
        /// Отмена доходит до самого перебора: уже отменённый токен
        /// останавливает поиск сразу, не дожидаясь исчерпания бюджета.
        /// </summary>
        [TestMethod]
        public void FindContours_CanceledToken_StopsImmediately()
        {
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            Assert.Throws<OperationCanceledException>(
                () => new DxfPointCycleFinder(0.001).FindContours(GridSegments(7), canceled.Token));
        }

        /// <summary>Решётка (n+1)×(n+1) узлов из единичных отрезков.</summary>
        private static List<Polyline2D> GridSegments(int n)
        {
            var segments = new List<Polyline2D>();
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    segments.Add(Segment((j, i), (j + 1, i)));
                    segments.Add(Segment((i, j), (i, j + 1)));
                }
            }

            return segments;
        }

        private static Polyline2D Segment(
            (double x, double y) start,
            (double x, double y) end)
            => new Polyline2D
            {
                Points = new List<Point2D>
                {
                    new Point2D { X = start.x, Y = start.y },
                    new Point2D { X = end.x, Y = end.y },
                },
            };

        private static bool ContainsPoint(IEnumerable<Point2D> points, double x, double y)
            => points.Any(point => System.Math.Abs(point.X - x) < 1e-9 &&
                                   System.Math.Abs(point.Y - y) < 1e-9);

        private static void AssertPoint(Point2D point, double x, double y)
        {
            Assert.AreEqual(x, point.X, 1e-9);
            Assert.AreEqual(y, point.Y, 1e-9);
        }
    }
}
