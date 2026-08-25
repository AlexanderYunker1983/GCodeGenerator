using System;
using System.IO;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Образцовые DXF-чертежи из <c>Tests/Assets</c>: описаны кодом
    /// (<see cref="DxfAssetWriter"/>) и перегенерируются по требованию.
    /// </summary>
    [TestClass]
    public class DxfAssetTests
    {
        private static string AssetsSourceDirectory =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets"));

        /// <summary>
        /// Перегенерация образцовых чертежей в исходный каталог.
        /// Выполняется только при GCG_WRITE_ASSETS=1 (в CI — no-op).
        /// </summary>
        [TestMethod]
        public void Write_Dxf_Assets()
        {
            if (Environment.GetEnvironmentVariable("GCG_WRITE_ASSETS") != "1")
                return;

            DxfAssetWriter.WriteAll(AssetsSourceDirectory);
        }
    }
}
