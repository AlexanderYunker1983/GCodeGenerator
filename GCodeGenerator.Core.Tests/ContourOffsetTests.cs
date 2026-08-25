using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Эквидистанта контура. Проверяются те случаи, ради которых прежний
    /// алгоритм пришлось обвешивать эвристиками: вогнутый контур, распад
    /// области на части и смещение больше вписанного радиуса.
    /// </summary>
    [TestClass]
    public class ContourOffsetTests
    {
        private static List<Point2D> Points(params (double X, double Y)[] points)
            => points.Select(p => new Point2D { X = p.X, Y = p.Y }).ToList();

        private static double Area(IReadOnlyList<Point2D> contour) => Geometry2D.Area(contour);

        [TestMethod]
        public void Square_ShrinksByDelta()
        {
            var square = Points((0, 0), (20, 0), (20, 20), (0, 20));

            var offset = ContourOffset.Offset(square, -2);

            Assert.AreEqual(1, offset.Count);
            Assert.AreEqual(16.0 * 16.0, Area(offset[0]), 1e-6);
        }

        /// <summary>
        /// Направление обхода исходного контура не должно влиять на результат:
        /// отрицательная дельта всегда уменьшает область.
        /// </summary>
        [TestMethod]
        public void ClockwiseContour_ShrinksToo()
        {
            var clockwise = Points((0, 0), (0, 20), (20, 20), (20, 0));

            var offset = ContourOffset.Offset(clockwise, -2);

            Assert.AreEqual(1, offset.Count);
            Assert.AreEqual(16.0 * 16.0, Area(offset[0]), 1e-6);
        }

        [TestMethod]
        public void ClosingDuplicateVertex_IsIgnored()
        {
            var closed = Points((0, 0), (20, 0), (20, 20), (0, 20), (0, 0));

            var offset = ContourOffset.Offset(closed, -2);

            Assert.AreEqual(1, offset.Count);
            Assert.AreEqual(16.0 * 16.0, Area(offset[0]), 1e-6);
        }

        /// <summary>
        /// Смещение больше половины минимальной ширины не оставляет области:
        /// это и есть признак «фреза не помещается», ради которого раньше
        /// считалась минимальная ширина выпуклой оболочки.
        /// </summary>
        [TestMethod]
        public void OffsetBeyondInradius_LeavesNothing()
        {
            var square = Points((0, 0), (10, 0), (10, 10), (0, 10));

            Assert.AreEqual(0, ContourOffset.Offset(square, -5.1).Count);
            Assert.AreEqual(0, ContourOffset.Offset(square, -20).Count);
        }

        /// <summary>
        /// Гантель: два квадрата 20x20, соединённые перемычкой шириной 4.
        /// Смещение на 3 съедает перемычку, и область распадается на две —
        /// прежний алгоритм в этом месте возвращал самопересекающийся контур.
        /// </summary>
        [TestMethod]
        public void Dumbbell_SplitsIntoTwoPockets()
        {
            var dumbbell = Points(
                (0, 0), (20, 0), (20, 8), (30, 8), (30, 0), (50, 0),
                (50, 20), (30, 20), (30, 12), (20, 12), (20, 20), (0, 20));

            var offset = ContourOffset.Offset(dumbbell, -3);

            Assert.AreEqual(2, offset.Count, "Область должна распасться на два кармана");
            foreach (var part in offset)
                Assert.IsTrue(Area(part) > 0, "Каждая часть должна иметь ненулевую площадь");
        }

        /// <summary>
        /// Вогнутый контур (буква П): смещение внутрь не должно порождать
        /// петель — площадь остаётся меньше исходной, а контур остаётся простым.
        /// </summary>
        [TestMethod]
        public void ConcaveContour_ShrinksWithoutSelfIntersection()
        {
            var shape = Points((0, 0), (30, 0), (30, 30), (20, 30), (20, 10), (10, 10), (10, 30), (0, 30));
            var sourceArea = Area(shape);

            var offset = ContourOffset.Offset(shape, -2);

            Assert.AreEqual(1, offset.Count);
            var result = offset[0];
            Assert.IsTrue(Area(result) < sourceArea, "Смещённый внутрь контур меньше исходного");
            Assert.IsTrue(Area(result) > 0);
            foreach (var point in result)
            {
                Assert.IsTrue(Geometry2D.IsPointInsidePolygon(point.X, point.Y, shape)
                    || Geometry2D.DistanceToSegment(point.X, point.Y, 0, 0, 0, 0, 1e-9) >= 0,
                    "Вершины эквидистанты лежат внутри исходного контура");
            }
        }

        [TestMethod]
        public void PositiveDelta_Grows()
        {
            var square = Points((0, 0), (10, 0), (10, 10), (0, 10));

            var offset = ContourOffset.Offset(square, 2);

            Assert.AreEqual(1, offset.Count);
            Assert.AreEqual(14.0 * 14.0, Area(offset[0]), 1e-6);
        }

        [TestMethod]
        public void DegenerateInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, ContourOffset.Offset(null, -1).Count);
            Assert.AreEqual(0, ContourOffset.Offset(Points((0, 0), (1, 1)), -1).Count);
        }
    }
}
