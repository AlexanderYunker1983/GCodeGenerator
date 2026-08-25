using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Общие плоские примитивы. Раньше каждая из этих формул существовала
    /// в двух-восьми копиях и проверялась только косвенно — через вывод
    /// генератора; здесь они закреплены напрямую, включая краевые случаи.
    /// </summary>
    [TestClass]
    public class Geometry2DTests
    {
        private static List<DxfPoint> Points(params (double X, double Y)[] points)
        {
            var result = new List<DxfPoint>(points.Length);
            foreach (var (x, y) in points)
                result.Add(new DxfPoint { X = x, Y = y });
            return result;
        }

        [TestMethod]
        public void PointsMatch_WithinTolerance_IsTrue()
        {
            var a = new DxfPoint { X = 10, Y = 5 };
            var b = new DxfPoint { X = 10.0005, Y = 5 };

            Assert.IsTrue(Geometry2D.PointsMatch(a, b, 1e-3));
            Assert.IsFalse(Geometry2D.PointsMatch(a, b, 1e-6));
        }

        /// <summary>
        /// Отсутствующая точка не совпадает ни с чем, включая другую
        /// отсутствующую: иначе разрыв контура выглядел бы как соединение.
        /// </summary>
        [TestMethod]
        public void PointsMatch_Null_IsFalse()
        {
            var point = new DxfPoint { X = 1, Y = 1 };

            Assert.IsFalse(Geometry2D.PointsMatch(null, point, 1));
            Assert.IsFalse(Geometry2D.PointsMatch(point, null, 1));
            Assert.IsFalse(Geometry2D.PointsMatch(null, null, 1));
        }

        [TestMethod]
        public void SignedArea_DependsOnWindingDirection()
        {
            var counterClockwise = Points((0, 0), (10, 0), (10, 10), (0, 10));
            var clockwise = Points((0, 0), (0, 10), (10, 10), (10, 0));

            Assert.AreEqual(100.0, Geometry2D.SignedArea(counterClockwise), 1e-9);
            Assert.AreEqual(-100.0, Geometry2D.SignedArea(clockwise), 1e-9);
            Assert.AreEqual(100.0, Geometry2D.Area(clockwise), 1e-9);
        }

        [TestMethod]
        public void SignedArea_DegenerateContour_IsZero()
        {
            Assert.AreEqual(0.0, Geometry2D.SignedArea(null));
            Assert.AreEqual(0.0, Geometry2D.SignedArea(Points((0, 0), (1, 1))));
        }

        [TestMethod]
        public void Centroid_Square_IsCenter()
        {
            var square = Points((0, 0), (4, 0), (4, 4), (0, 4));

            var (x, y) = Geometry2D.Centroid(square, 1e-6);

            Assert.AreEqual(2.0, x, 1e-9);
            Assert.AreEqual(2.0, y, 1e-9);
        }

        /// <summary>
        /// У контура нулевой площади (все точки на одной прямой) делить не на
        /// что: центр берётся как среднее арифметическое вершин.
        /// </summary>
        [TestMethod]
        public void Centroid_ZeroArea_FallsBackToVertexAverage()
        {
            var collinear = Points((0, 0), (10, 0), (20, 0));

            var (x, y) = Geometry2D.Centroid(collinear, 1e-6);

            Assert.AreEqual(10.0, x, 1e-9);
            Assert.AreEqual(0.0, y, 1e-9);
        }

        [TestMethod]
        public void IsPointInsidePolygon_InsideAndOutside()
        {
            var square = Points((0, 0), (10, 0), (10, 10), (0, 10));

            Assert.IsTrue(Geometry2D.IsPointInsidePolygon(5, 5, square));
            Assert.IsFalse(Geometry2D.IsPointInsidePolygon(15, 5, square));
            Assert.IsFalse(Geometry2D.IsPointInsidePolygon(5, 5, Points((0, 0), (1, 0))));
        }

        /// <summary>
        /// Невыпуклый контур: точка в «вырезе» буквы П снаружи, хотя и попадает
        /// в габаритный прямоугольник.
        /// </summary>
        [TestMethod]
        public void IsPointInsidePolygon_ConcaveNotch_IsOutside()
        {
            var shape = Points((0, 0), (10, 0), (10, 10), (7, 10), (7, 3), (3, 3), (3, 10), (0, 10));

            Assert.IsTrue(Geometry2D.IsPointInsidePolygon(5, 1, shape));
            Assert.IsFalse(Geometry2D.IsPointInsidePolygon(5, 7, shape));
        }

        [TestMethod]
        public void DistanceToSegment_ProjectionInsideAndOutside()
        {
            Assert.AreEqual(4.0, Geometry2D.DistanceToSegment(5, 4, 0, 0, 10, 0, 1e-9), 1e-9);
            Assert.AreEqual(5.0, Geometry2D.DistanceToSegment(13, 4, 0, 0, 10, 0, 1e-9), 1e-9);
        }

        [TestMethod]
        public void DistanceToSegment_DegenerateSegment_UsesEndpoint()
        {
            Assert.AreEqual(5.0, Geometry2D.DistanceToSegment(3, 4, 0, 0, 0, 0, 1e-9), 1e-9);
        }

        [TestMethod]
        public void SegmentIntersection_CrossingSegments()
        {
            var point = Geometry2D.SegmentIntersection(0, 0, 10, 10, 0, 10, 10, 0, 1e-9, 1e-6);

            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(5.0, point.Value.x, 1e-9);
            Assert.AreEqual(5.0, point.Value.y, 1e-9);
        }

        [TestMethod]
        public void SegmentIntersection_Parallel_IsNull()
        {
            Assert.IsNull(Geometry2D.SegmentIntersection(0, 0, 10, 0, 0, 5, 10, 5, 1e-9, 1e-6));
        }

        /// <summary>
        /// Прямые пересекаются, но точка пересечения лежит за концами отрезков —
        /// это не пересечение отрезков.
        /// </summary>
        [TestMethod]
        public void SegmentIntersection_BeyondSegmentBounds_IsNull()
        {
            Assert.IsNull(Geometry2D.SegmentIntersection(0, 0, 1, 0, 5, -5, 5, 5, 1e-9, 1e-6));
        }

        [TestMethod]
        public void SegmentIntersectionPoint_ReturnsContourPoint()
        {
            var point = Geometry2D.SegmentIntersectionPoint(0, 0, 10, 0, 5, -5, 5, 5, 1e-9, 1e-6);

            Assert.IsNotNull(point);
            Assert.AreEqual(5.0, point.X, 1e-9);
            Assert.AreEqual(0.0, point.Y, 1e-9);
            Assert.IsNull(Geometry2D.SegmentIntersectionPoint(0, 0, 10, 0, 0, 5, 10, 5, 1e-9, 1e-6));
        }
    }
}
