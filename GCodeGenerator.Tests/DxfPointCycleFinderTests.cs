using System.Collections.Generic;
using System.Linq;
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
