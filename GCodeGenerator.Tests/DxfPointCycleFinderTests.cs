using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfPointCycleFinderTests
    {
        [TestMethod]
        public void FindContours_SquareGraph_ReturnsOneUniqueClosedCycle()
        {
            var segments = new List<DxfPolyline>
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

        private static DxfPolyline Segment(
            (double x, double y) start,
            (double x, double y) end)
            => new DxfPolyline
            {
                Points = new List<DxfPoint>
                {
                    new DxfPoint { X = start.x, Y = start.y },
                    new DxfPoint { X = end.x, Y = end.y },
                },
            };

        private static bool ContainsPoint(IEnumerable<DxfPoint> points, double x, double y)
            => points.Any(point => System.Math.Abs(point.X - x) < 1e-9 &&
                                   System.Math.Abs(point.Y - y) < 1e-9);

        private static void AssertPoint(DxfPoint point, double x, double y)
        {
            Assert.AreEqual(x, point.X, 1e-9);
            Assert.AreEqual(y, point.Y, 1e-9);
        }
    }
}
