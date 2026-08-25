using System.Collections.Generic;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfSegmentIntersectionSplitterTests
    {
        [TestMethod]
        public void Split_CrossingSegments_InsertsSharedIntersectionInPolylineOrder()
        {
            var horizontal = Polyline((0, 5), (10, 5));
            var vertical = Polyline((5, 0), (5, 10));

            var result = new DxfSegmentIntersectionSplitter(0.001)
                .Split(new List<DxfPolyline> { horizontal, vertical });

            Assert.AreEqual(4, result.Count);
            AssertSegment(result[0], (0, 5), (5, 5));
            AssertSegment(result[1], (5, 5), (10, 5));
            AssertSegment(result[2], (5, 0), (5, 5));
            AssertSegment(result[3], (5, 5), (5, 10));
        }

        private static DxfPolyline Polyline(params (double x, double y)[] points)
        {
            var polyline = new DxfPolyline();
            foreach (var point in points)
                polyline.Points.Add(new DxfPoint { X = point.x, Y = point.y });
            return polyline;
        }

        private static void AssertSegment(
            DxfPolyline segment,
            (double x, double y) start,
            (double x, double y) end)
        {
            Assert.AreEqual(2, segment.Points.Count);
            Assert.AreEqual(start.x, segment.Points[0].X, 1e-9);
            Assert.AreEqual(start.y, segment.Points[0].Y, 1e-9);
            Assert.AreEqual(end.x, segment.Points[1].X, 1e-9);
            Assert.AreEqual(end.y, segment.Points[1].Y, 1e-9);
        }
    }
}
