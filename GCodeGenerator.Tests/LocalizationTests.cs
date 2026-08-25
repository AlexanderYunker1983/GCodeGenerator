using System;
using System.Globalization;
using GCodeGenerator.Localization;
using GCodeGenerator.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты локализации: английский — нейтральный набор строк
    /// (LocalizableResources.resx, лежит в самой сборке), русский — сателлит
    /// (LocalizableResources.ru.resx); отсутствующий ключ даёт «?key?» и запись
    /// в журнал.
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

        /// <summary>Русский сателлит — перевод для культуры ru.</summary>
        [TestMethod]
        public void GetString_RuCulture_ReturnsRussianFromSatellite()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("ru"));

            Assert.AreEqual("Генератор G-кода", manager.GetString("MainTitle"));
            Assert.AreEqual("Сверление по линии", manager.GetString("AddDrillLine"));
        }

        /// <summary>
        /// Английский — нейтральный набор: он же достаётся любой культуре,
        /// для которой перевода нет.
        /// </summary>
        [TestMethod]
        public void GetString_EnCulture_ReturnsEnglishFromNeutralResx()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("en"));

            Assert.AreEqual("G-code Generator", manager.GetString("MainTitle"));
            Assert.AreEqual("Drilling along a line", manager.GetString("AddDrillLine"));
            Assert.AreEqual("Spindle speed, RPM", manager.GetString("SpindleSpeedRpm"));
        }

        /// <summary>Форматирование параметров работает и в нейтральном наборе.</summary>
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

        /// <summary>
        /// Культура без перевода получает английский, а не русский: набор
        /// строк в самой сборке — нейтральный, и именно он достаётся всем,
        /// для кого сателлита нет. Прежде нейтральным был русский, и станок
        /// с немецкой или китайской системой показывал русский интерфейс.
        /// </summary>
        [TestMethod]
        public void GetString_CultureWithoutTranslation_FallsBackToEnglish()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("de"));

            Assert.AreEqual("G-code Generator", manager.GetString("MainTitle"));
        }

        /// <summary>
        /// Название новой операции берётся из словаря по ключу каталога.
        /// Ключ живёт рядом с типом операции, а словарь — в приложении,
        /// поэтому проверка здесь: тип, добавленный в каталог без перевода,
        /// появлялся бы в списке операций под «?ключ?».
        /// </summary>
        [TestMethod]
        public void EveryCatalogType_HasTranslatedDefaultName()
        {
            foreach (var culture in new[] { "ru", "en" })
            {
                var manager = CreateManager();
                manager.ChangeCulture(new CultureInfo(culture));

                foreach (var descriptor in OperationCatalog.All)
                {
                    Assert.AreEqual(descriptor.PersistentName + "Name", descriptor.NameKey);

                    var name = manager.GetString(descriptor.NameKey);

                    Assert.IsFalse(string.IsNullOrWhiteSpace(name), descriptor.NameKey);
                    Assert.IsFalse(name.StartsWith("?", StringComparison.Ordinal),
                        $"{culture}: нет перевода названия {descriptor.NameKey}");
                }
            }
        }
    }
}
