using System;
using System.Collections.Generic;
using System.IO;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Загрузка образцовых DXF-файлов (Assets/*.dxf) через реальные парсеры продукта:
    /// <see cref="ProfileDxfOperationViewModel.ParseDxfLines"/> и
    /// <see cref="PocketDxfOperationViewModel.ParseDxfClosedContours"/>
    /// (internal, открыты через InternalsVisibleTo — пункт 0.3 плана).
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
            var vm = new ProfileDxfOperationViewModel();
            return vm.ParseDxfLines(GetAssetPath(fileName));
        }

        /// <summary>
        /// Парсит DXF кармана (только замкнутые контуры).
        /// </summary>
        public static List<DxfPolyline> LoadPocketClosedContours(string fileName)
        {
            var vm = new PocketDxfOperationViewModel();
            return vm.ParseDxfClosedContours(GetAssetPath(fileName));
        }
    }
}
