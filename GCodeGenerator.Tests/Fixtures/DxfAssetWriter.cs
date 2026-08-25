using System.IO;
using netDxf;
using netDxf.Entities;
using netDxf.Header;
using netDxf.Units;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Генерация образцовых DXF-чертежей из <c>Tests/Assets</c>.
    ///
    /// Прежние ассеты писались вручную и содержали только секцию ENTITIES:
    /// самодельному разбору этого хватало, но настоящий DXF так не выглядит —
    /// в нём есть шапка с версией и единицами, а также таблицы слоёв и типов
    /// линий. Здесь те же чертежи описаны кодом и сохраняются полноценным
    /// файлом, поэтому тесты работают на таком же файле, какой приходит
    /// из CAD-системы.
    ///
    /// Геометрия совпадает с прежними ассетами координата в координату,
    /// поэтому эталонные программы не меняются.
    /// </summary>
    internal static class DxfAssetWriter
    {
        /// <summary>Записывает все образцовые чертежи в указанный каталог.</summary>
        internal static void WriteAll(string directory)
        {
            Directory.CreateDirectory(directory);
            WriteCircleSample(Path.Combine(directory, "circle_sample.dxf"));
            WriteLwPolylineSample(Path.Combine(directory, "lwpolyline_sample.dxf"));
            WriteProfileSample(Path.Combine(directory, "profile_sample.dxf"));
            WritePocketSample(Path.Combine(directory, "pocket_sample.dxf"));
        }

        /// <summary>Окружность радиусом 2 с центром (5, 5).</summary>
        private static void WriteCircleSample(string path)
        {
            var document = CreateDocument();
            document.Entities.Add(new Circle(new Vector2(5, 5), 2));
            document.Save(path);
        }

        /// <summary>Замкнутая полилиния 10×5 из четырёх вершин.</summary>
        private static void WriteLwPolylineSample(string path)
        {
            var document = CreateDocument();
            var polyline = new Polyline2D(new[]
            {
                new Polyline2DVertex(0, 0),
                new Polyline2DVertex(10, 0),
                new Polyline2DVertex(10, 5),
                new Polyline2DVertex(0, 5)
            })
            {
                IsClosed = true
            };
            document.Entities.Add(polyline);
            document.Save(path);
        }

        /// <summary>Контур «D»: три отрезка и дуга радиусом 10 (270° → 90°).</summary>
        private static void WriteProfileSample(string path)
        {
            var document = CreateDocument();
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(30, 0)));
            document.Entities.Add(new Arc(new Vector2(30, 10), 10, 270, 90));
            document.Entities.Add(new Line(new Vector2(30, 20), new Vector2(0, 20)));
            document.Entities.Add(new Line(new Vector2(0, 20), new Vector2(0, 0)));
            document.Save(path);
        }

        /// <summary>Два замкнутых контура из отрезков: 40×20 и 12×12.</summary>
        private static void WritePocketSample(string path)
        {
            var document = CreateDocument();
            document.Entities.Add(new Line(new Vector2(0, 0), new Vector2(40, 0)));
            document.Entities.Add(new Line(new Vector2(40, 0), new Vector2(40, 20)));
            document.Entities.Add(new Line(new Vector2(40, 20), new Vector2(0, 20)));
            document.Entities.Add(new Line(new Vector2(0, 20), new Vector2(0, 0)));
            document.Entities.Add(new Line(new Vector2(60, 0), new Vector2(72, 0)));
            document.Entities.Add(new Line(new Vector2(72, 0), new Vector2(72, 12)));
            document.Entities.Add(new Line(new Vector2(72, 12), new Vector2(60, 12)));
            document.Entities.Add(new Line(new Vector2(60, 12), new Vector2(60, 0)));
            document.Save(path);
        }

        private static DxfDocument CreateDocument()
        {
            var document = new DxfDocument(DxfVersion.AutoCad2000);
            document.DrawingVariables.InsUnits = DrawingUnits.Millimeters;
            return document;
        }
    }
}
