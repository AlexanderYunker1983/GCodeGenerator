using System;
using System.IO;
using System.Linq;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using netDxf;
using netDxf.Blocks;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Units;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Разбор DXF: случаи, которые самодельный построчный разбор не покрывал —
    /// дуги внутри полилинии, единицы чертежа, вставки блоков и повреждённый
    /// файл.
    /// </summary>
    [TestClass]
    public class DxfEntityReaderTests
    {
        private string _path;

        [TestInitialize]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), $"gcodegen_reader_{Guid.NewGuid():N}.dxf");
        }

        [TestCleanup]
        public void TearDown()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        private static DxfDocument NewDocument(DrawingUnits units = DrawingUnits.Millimeters)
        {
            var document = new DxfDocument(DxfVersion.AutoCad2000);
            document.DrawingVariables.InsUnits = units;
            return document;
        }

        private static double Width(System.Collections.Generic.List<GCodeGenerator.Models.DxfPolyline> polylines)
        {
            var xs = polylines.SelectMany(p => p.Points).Select(p => p.X).ToList();
            return xs.Max() - xs.Min();
        }

        /// <summary>
        /// Дуга внутри полилинии задаётся параметром bulge. Прежний разбор его
        /// не читал, и дуга молча превращалась в хорду: вместо полуокружности
        /// радиусом 5 получался отрезок.
        /// </summary>
        [TestMethod]
        public void Polyline_WithBulge_IsExpandedToArc()
        {
            var document = NewDocument();
            var polyline = new Polyline2D(new[]
            {
                new Polyline2DVertex(0, 0) { Bulge = 1.0 },
                new Polyline2DVertex(10, 0)
            });
            document.Entities.Add(polyline);
            document.Save(_path);

            var polylines = DxfImportServiceProbe.ReadProfile(_path);

            Assert.AreEqual(1, polylines.Count);
            var points = polylines[0].Points;
            Assert.IsTrue(points.Count > 2, "Дуга должна разбиваться на сегменты, а не оставаться хордой");
            var maxY = points.Max(p => Math.Abs(p.Y));
            Assert.AreEqual(5.0, maxY, 0.2, "Полуокружность радиусом 5 отходит от хорды на 5 мм");
        }

        /// <summary>
        /// Чертёж в дюймах приводится к миллиметрам: иначе деталь шириной
        /// 10 дюймов фрезеровалась бы как 10 мм.
        /// </summary>
        [TestMethod]
        public void Drawing_InInches_IsConvertedToMillimeters()
        {
            var document = NewDocument(DrawingUnits.Inches);
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)));
            document.Save(_path);

            var polylines = DxfImportServiceProbe.ReadProfile(_path);

            Assert.AreEqual(1, polylines.Count);
            Assert.AreEqual(254.0, Width(polylines), 1e-6);
        }

        [TestMethod]
        public void Drawing_InMillimeters_KeepsCoordinates()
        {
            var document = NewDocument();
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)));
            document.Save(_path);

            Assert.AreEqual(10.0, Width(DxfImportServiceProbe.ReadProfile(_path)), 1e-6);
        }

        /// <summary>
        /// Геометрия внутри вставленного блока — часть чертежа. Прежний разбор
        /// читал определение блока как обычные сущности, игнорируя смещение
        /// вставки, поэтому деталь оказывалась не на своём месте.
        /// </summary>
        [TestMethod]
        public void BlockInsert_IsExplodedAtInsertPosition()
        {
            var document = NewDocument();
            var block = new Block("frame");
            block.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)));
            document.Entities.Add(new Insert(block, new Vector2(100, 50)));
            document.Save(_path);

            var polylines = DxfImportServiceProbe.ReadProfile(_path);

            Assert.AreEqual(1, polylines.Count);
            var points = polylines[0].Points;
            Assert.AreEqual(100.0, points.Min(p => p.X), 1e-6, "Смещение вставки учтено");
            Assert.AreEqual(110.0, points.Max(p => p.X), 1e-6);
            Assert.AreEqual(50.0, points[0].Y, 1e-6);
        }

        /// <summary>
        /// Полилинии — обычная геометрия контура и должны попадать
        /// в профильную операцию: прежде профильный импорт их отбрасывал,
        /// и нарисованный полилинией контур просто не появлялся.
        /// </summary>
        [TestMethod]
        public void ProfileImport_IncludesPolylines()
        {
            var document = NewDocument();
            document.Entities.Add(new Polyline2D(new[]
            {
                new Polyline2DVertex(0, 0),
                new Polyline2DVertex(10, 0),
                new Polyline2DVertex(10, 10)
            }));
            document.Save(_path);

            var polylines = DxfImportServiceProbe.ReadProfile(_path);

            Assert.AreEqual(1, polylines.Count);
            Assert.AreEqual(3, polylines[0].Points.Count);
        }

        /// <summary>
        /// Замкнутая полилиния возвращается с повторением первой вершины:
        /// на этом строится распознавание замкнутых контуров кармана.
        /// </summary>
        [TestMethod]
        public void ClosedPolyline_RepeatsFirstVertex()
        {
            var document = NewDocument();
            document.Entities.Add(new Polyline2D(new[]
            {
                new Polyline2DVertex(0, 0),
                new Polyline2DVertex(10, 0),
                new Polyline2DVertex(10, 10),
                new Polyline2DVertex(0, 10)
            })
            {
                IsClosed = true
            });
            document.Save(_path);

            var points = DxfImportServiceProbe.ReadProfile(_path)[0].Points;

            Assert.AreEqual(5, points.Count);
            Assert.AreEqual(points[0].X, points[4].X, 1e-9);
            Assert.AreEqual(points[0].Y, points[4].Y, 1e-9);
        }

        /// <summary>
        /// Повреждённый файл должен приводить к ошибке импорта, а не к тихо
        /// испорченной геометрии: прежний разбор превращал неразобранное
        /// число в координату 0.
        /// </summary>
        [TestMethod]
        public void CorruptedFile_ThrowsInsteadOfSilentGeometry()
        {
            File.WriteAllText(_path, "это не DXF-файл");

            Assert.ThrowsException<netDxf.IO.DxfVersionNotSupportedException>(
                () => DxfImportServiceProbe.ReadProfile(_path));
        }

        /// <summary>Текстовые и размерные сущности контуром не являются.</summary>
        [TestMethod]
        public void NonGeometryEntities_AreIgnored()
        {
            var document = NewDocument();
            document.Entities.Add(new Text("подпись", new Vector2(0, 0), 2.5));
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(10, 0)));
            document.Save(_path);

            Assert.AreEqual(1, DxfImportServiceProbe.ReadProfile(_path).Count);
        }

        private static class DxfImportServiceProbe
        {
            private static readonly DxfImportService Service = new DxfImportService();

            public static System.Collections.Generic.List<GCodeGenerator.Models.DxfPolyline> ReadProfile(string path)
                => Service.ReadProfilePolylines(path);
        }
    }
}
