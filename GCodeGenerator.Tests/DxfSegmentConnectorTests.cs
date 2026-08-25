using System.Collections.Generic;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfSegmentConnectorTests
    {
        [TestMethod]
        public void Connect_ReversedSquareSegment_ProducesOneClosedOrderedContour()
        {
            var segments = new List<Polyline2D>
            {
                Segment((0, 0), (10, 0)),
                Segment((10, 10), (10, 0)),
                Segment((10, 10), (0, 10)),
                Segment((0, 10), (0, 0)),
            };

            var contours = new DxfSegmentConnector(0.001).Connect(segments);

            Assert.AreEqual(1, contours.Count);
            var points = contours[0].Points;
            Assert.AreEqual(5, points.Count);
            AssertPoint(points[0], 0, 0);
            AssertPoint(points[1], 10, 0);
            AssertPoint(points[2], 10, 10);
            AssertPoint(points[3], 0, 10);
            AssertPoint(points[4], 0, 0);
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

        private static void AssertPoint(Point2D point, double x, double y)
        {
            Assert.AreEqual(x, point.X, 1e-9);
            Assert.AreEqual(y, point.Y, 1e-9);
        }
    }
}
