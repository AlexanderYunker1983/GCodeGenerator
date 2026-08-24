using System.Linq;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfImportServiceTests
    {
        [TestMethod]
        public void PocketImport_ClosedLwPolyline_PreservesEveryVertex()
        {
            var contours = DxfFixtureLoader.LoadPocketClosedContours("lwpolyline_sample.dxf");
            var contour = contours.FirstOrDefault(candidate => candidate.Points?.Count == 5);

            Assert.IsNotNull(contour, "Четыре вершины и замыкающая точка должны сохраниться");
            Assert.AreEqual(0.0, contour.Points[0].X, 1e-9);
            Assert.AreEqual(0.0, contour.Points[0].Y, 1e-9);
            Assert.AreEqual(10.0, contour.Points[1].X, 1e-9);
            Assert.AreEqual(0.0, contour.Points[1].Y, 1e-9);
            Assert.AreEqual(10.0, contour.Points[2].X, 1e-9);
            Assert.AreEqual(5.0, contour.Points[2].Y, 1e-9);
            Assert.AreEqual(0.0, contour.Points[3].X, 1e-9);
            Assert.AreEqual(5.0, contour.Points[3].Y, 1e-9);
            Assert.AreEqual(contour.Points[0].X, contour.Points[4].X, 1e-9);
            Assert.AreEqual(contour.Points[0].Y, contour.Points[4].Y, 1e-9);
        }
    }
}
