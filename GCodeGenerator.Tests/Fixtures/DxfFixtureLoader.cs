using System;
using System.Collections.Generic;
using System.IO;
using GCodeGenerator.Models;
using GCodeGenerator.Import;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Загрузка образцовых DXF-файлов (Assets/*.dxf) через реальный сервис продукта.
    /// </summary>
    public static class DxfFixtureLoader
    {
        /// <summary>Каталог Assets в каталоге сборки тестов.</summary>
        public static string AssetsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");

        public static string GetAssetPath(string fileName)
        {
            return Path.Combine(AssetsDirectory, fileName);
        }

        /// <summary>
        /// Парсит DXF профиля (открытые контуры: LINE/ARC/CIRCLE/ELLIPSE).
        /// </summary>
        public static List<DxfPolyline> LoadProfilePolylines(string fileName)
        {
            return new DxfImportService().ReadProfilePolylines(GetAssetPath(fileName));
        }

        /// <summary>
        /// Парсит DXF кармана (только замкнутые контуры).
        /// </summary>
        public static List<DxfPolyline> LoadPocketClosedContours(string fileName)
        {
            return new DxfImportService().ReadPocketClosedContours(GetAssetPath(fileName));
        }
    }
}
