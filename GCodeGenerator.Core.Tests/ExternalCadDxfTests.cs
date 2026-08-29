using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Проверка DXF, которые выпущены реальным CAD-приложением, а не записаны
    /// библиотекой netDxf, используемой самим импортёром.
    /// </summary>
    [TestClass]
    public sealed class ExternalCadDxfTests
    {
        private static string FixturesDirectory => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "CadDxf");

        [TestMethod]
        [DataRow("librecad-square.dxf", "69031DD7A93E9D429EE6EFBDEB3AD49CCC6CB73A1ACF95D16F0574B4C075DD80")]
        [DataRow("librecad-block4-lwpolyline.dxf", "FDA808DE85DB50622F3BDE2AD1B75527D0C366FAC7D06E0DABE614BB0A145286")]
        [DataRow("librecad-v32-lwpolyline.dxf", "285755532D9A433D4FFA59206C907EC48902D7317B0BBCDB07BBD0BF4676732B")]
        public void Fixtures_MatchPinnedLibreCadSources(string fileName, string expectedSha256)
        {
            var path = Path.Combine(FixturesDirectory, fileName);

            Assert.IsTrue(File.Exists(path), $"Не найдена внешняя DXF-фикстура {fileName}");
            var normalizedText = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
            var actualSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedText)));
            Assert.AreEqual(expectedSha256, actualSha256,
                $"Содержимое {fileName} должно совпадать с закреплённым файлом LibreCAD без учёта CRLF/LF");
        }

        [TestMethod]
        public void MillimeterLwPolylines_FromLibreCad_AreImported()
        {
            var path = Path.Combine(FixturesDirectory, "librecad-v32-lwpolyline.dxf");

            var polylines = new DxfImportService().ReadProfilePolylines(path);

            Assert.AreEqual(5, polylines.Count,
                "Все пять LWPOLYLINE из секции ENTITIES должны пройти внешний CAD-маршрут");
            Assert.IsTrue(polylines.TrueForAll(polyline => polyline.Points.Count == 2));
            Assert.AreEqual(0.6, polylines[0].Points[1].Y, 1e-9,
                "INSUNITS=4 означает миллиметры и не должен менять координату");
        }

        [TestMethod]
        [DataRow("librecad-square.dxf", CoreErrorCodes.DxfNotADrawing)]
        [DataRow("librecad-block4-lwpolyline.dxf", CoreErrorCodes.DxfUnitsNotSpecified)]
        public void AmbiguousLibreCadDrawings_AreRejectedWithExpectedError(
            string fileName,
            string expectedErrorCode)
        {
            var path = Path.Combine(FixturesDirectory, fileName);

            var failure = Assert.Throws<CoreException>(
                () => new DxfImportService().ReadProfilePolylines(path));

            Assert.AreEqual(expectedErrorCode, failure.Code,
                "Неоднозначный внешний DXF нельзя молча трактовать как безопасный миллиметровый чертёж");
        }
    }
}
