using System.Collections.Generic;
using System.IO;
using GCodeGenerator.Import;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using netDxf;
using netDxf.Entities;
using netDxf.Units;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Импорт сплайнов из чертежа. Сплайн — обычный результат экспорта
    /// из векторных редакторов и CAD, и прежде разбор его молча пропускал:
    /// в списке поддержанных сущностей сплайна не было, и контур, где хотя
    /// бы одно ребро нарисовано сплайном, терял это ребро — карман отвечал
    /// «нет замкнутых контуров», не называя причину.
    /// </summary>
    [TestClass]
    public class DxfSplineImportTests
    {
        private static string SaveTemp(DxfDocument document)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dxf");
            document.Save(path);
            return path;
        }

        /// <summary>Открытый сплайн разбивается на хорды по всей длине кривой.</summary>
        [TestMethod]
        public void Read_OpenSpline_BecomesDensePolyline()
        {
            var document = new DxfDocument();
            document.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
            document.Entities.Add(new Spline(new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(10, 15, 0),
                new Vector3(20, -5, 0),
                new Vector3(30, 10, 0),
            }));
            var path = SaveTemp(document);
            try
            {
                var polylines = new DxfImportService().ReadProfilePolylines(path);

                Assert.AreEqual(1, polylines.Count, "сплайн должен стать одной ломаной");
                var points = polylines[0].Points;
                Assert.IsTrue(points.Count > 16,
                    $"сплайн разбит на {points.Count} точек — ожидались хорды по длине кривой, а не контрольные точки");
                Assert.AreEqual(0.0, points[0].X, 1e-6);
                Assert.AreEqual(0.0, points[0].Y, 1e-6);
                Assert.AreEqual(30.0, points[points.Count - 1].X, 1e-6);
                Assert.AreEqual(10.0, points[points.Count - 1].Y, 1e-6);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Контур, три стороны которого нарисованы отрезками, а четвёртая —
        /// сплайном, замыкается в контур кармана. Ровно этот случай прежде
        /// разваливался тише всего: три ребра на месте, четвёртое молча
        /// пропало, цикл не замкнулся.
        /// </summary>
        [TestMethod]
        public void PocketContours_SplineEdgeClosesContourDrawnWithLines()
        {
            var document = new DxfDocument();
            document.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
            document.Entities.Add(new Line(new Vector3(0, 0, 0), new Vector3(30, 0, 0)));
            document.Entities.Add(new Line(new Vector3(30, 0, 0), new Vector3(30, 20, 0)));
            document.Entities.Add(new Line(new Vector3(30, 20, 0), new Vector3(0, 20, 0)));
            document.Entities.Add(new Spline(new List<Vector3>
            {
                new Vector3(0, 20, 0),
                new Vector3(-4, 10, 0),
                new Vector3(0, 0, 0),
            }));
            var path = SaveTemp(document);
            try
            {
                var contours = new DxfImportService().ReadPocketClosedContours(path);

                Assert.IsTrue(contours.Count >= 1,
                    "контур с ребром-сплайном обязан замкнуться; прежде разбор терял сплайн и контуров не находил");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
