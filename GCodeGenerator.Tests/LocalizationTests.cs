using System.Globalization;
using GCodeGenerator.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты локализации (пункт 8.3 плана): resx на культуру (RU — нейтральный,
    /// EN — сателлит LocalizableResources.en.resx), «?key?» + лог при отсутствии ключа.
    /// </summary>
    [TestClass]
    public class LocalizationTests
    {
        private static LocalizationManager CreateManager()
        {
            var manager = new LocalizationManager();
            manager.AddAssembly("GCodeGenerator");
            return manager;
        }

        /// <summary>
        /// Нейтральный resx (RU) — значения по умолчанию для культур без сателлита.
        /// </summary>
        [TestMethod]
        public void GetString_RuCulture_ReturnsRussianFromNeutralResx()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("ru"));

            Assert.AreEqual("Генератор G-кода", manager.GetString("MainTitle"));
            Assert.AreEqual("Сверление по линии", manager.GetString("AddDrillLine"));
        }

        /// <summary>
        /// EN-сателлит (LocalizableResources.en.resx) — английские значения
        /// для культуры en.
        /// </summary>
        [TestMethod]
        public void GetString_EnCulture_ReturnsEnglishFromSatellite()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("en"));

            Assert.AreEqual("G-code Generator", manager.GetString("MainTitle"));
            Assert.AreEqual("Drilling along a line", manager.GetString("AddDrillLine"));
            Assert.AreEqual("Spindle speed, RPM", manager.GetString("SpindleSpeedRpm"));
        }

        /// <summary>
        /// Форматирование параметров сохраняется и в EN-сателлите.
        /// </summary>
        [TestMethod]
        public void GetString_EnCulture_FormatsParameters()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("en"));

            Assert.AreEqual("Lines imported: 42", manager.GetString("DxfImportInfo", 42));
        }

        /// <summary>
        /// Отсутствующий ключ → «?key?» (пункт 8.3: лог + сам ключ;
        /// захардкоженные фолбэки в VM удалены).
        /// </summary>
        [TestMethod]
        public void GetString_MissingKey_ReturnsQuestionMarkedKey()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("en"));

            Assert.AreEqual("?NoSuchKey?", manager.GetString("NoSuchKey"));
        }
    }
}
