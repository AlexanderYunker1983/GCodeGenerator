using System;
using System.Collections.Generic;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfSegmentIntersectionDetectorTests
    {
        [TestMethod]
        public void FindIntersections_MultiSegmentCross_DeduplicatesSharedVertex()
        {
            var horizontal = Polyline((-1, 0), (0, 0), (1, 0));
            var vertical = Polyline((0, -1), (0, 0), (0, 1));

            var intersections = new DxfSegmentIntersectionDetector(0.001)
                .FindIntersections(horizontal, vertical);

            Assert.AreEqual(1, intersections.Count);
            Assert.AreEqual(0, intersections[0].X, 1e-9);
            Assert.AreEqual(0, intersections[0].Y, 1e-9);
        }

        [TestMethod]
        public void DistanceToSegment_ProjectionPastEnd_UsesNearestEndpoint()
        {
            var distance = new DxfSegmentIntersectionDetector(0.001)
                .DistanceToSegment(15, 4, 0, 0, 10, 0);

            Assert.AreEqual(Math.Sqrt(41), distance, 1e-9);
        }

        private static Polyline2D Polyline(params (double x, double y)[] points)
        {
            var result = new Polyline2D { Points = new List<Point2D>() };
            foreach (var point in points)
                result.Points.Add(new Point2D { X = point.x, Y = point.y });
            return result;
        }
    }
}
