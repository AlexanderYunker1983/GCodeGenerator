using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfPolylineIntersectionPipelineTests
    {
        [TestMethod]
        public void Collect_MultipleCrossings_SortsPointsByDistanceAlongPolyline()
        {
            var detector = new DxfSegmentIntersectionDetector(0.001);
            var collector = new DxfPolylineIntersectionCollector(0.001, detector);
            var polylines = new List<DxfPolyline>
            {
                Segment((0, 0), (10, 0)),
                Segment((8, -1), (8, 1)),
                Segment((2, -1), (2, 1)),
            };

            var result = collector.Collect(polylines);

            Assert.AreEqual(2, result[0].Count);
            Assert.AreEqual(2, result[0][0].Point.X, 1e-9);
            Assert.AreEqual(2, result[0][0].Distance, 1e-9);
            Assert.AreEqual(8, result[0][1].Point.X, 1e-9);
            Assert.AreEqual(8, result[0][1].Distance, 1e-9);
        }

        [TestMethod]
        public void Subdivide_OrderedIntersections_EmitsConsecutiveGraphEdges()
        {
            var detector = new DxfSegmentIntersectionDetector(0.001);
            var subdivider = new DxfPolylineSubdivider(0.001, detector);
            var polylines = new List<DxfPolyline>
            {
                Segment((0, 0), (10, 0)),
            };
            var intersections = new Dictionary<int, List<DxfPolylineIntersection>>
            {
                [0] = new List<DxfPolylineIntersection>
                {
                    new DxfPolylineIntersection(new DxfPoint { X = 3, Y = 0 }, 3),
                    new DxfPolylineIntersection(new DxfPoint { X = 7, Y = 0 }, 7),
                },
            };

            var result = subdivider.Subdivide(polylines, intersections);

            Assert.AreEqual(3, result.Count);
            AssertEdge(result[0], 0, 3);
            AssertEdge(result[1], 3, 7);
            AssertEdge(result[2], 7, 10);
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

        private static void AssertEdge(DxfPolyline edge, double startX, double endX)
        {
            Assert.AreEqual(2, edge.Points.Count);
            Assert.AreEqual(startX, edge.Points[0].X, 1e-9);
            Assert.AreEqual(endX, edge.Points[1].X, 1e-9);
        }
    }
}
