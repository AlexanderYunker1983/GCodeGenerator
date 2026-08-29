using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            Assert.AreEqual("Z, мм", manager.GetString("MachineAxisZMillimeters"));
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
            Assert.AreEqual("Z, mm", manager.GetString("MachineAxisZMillimeters"));
        }

        /// <summary>Форматирование параметров работает и в нейтральном наборе.</summary>
        [TestMethod]
        public void GetString_EnCulture_FormatsParameters()
        {
            var manager = CreateManager();
            manager.ChangeCulture(new CultureInfo("en"));

            Assert.AreEqual("Lines imported: 42", manager.GetString("DxfImportInfo", 42));
        }

        [TestMethod]
        public void SecondInstanceFailure_UsesPersistedInterfaceLanguage()
        {
            Assert.AreEqual(
                "GCodeGenerator уже запущен, но передать запрос работающему экземпляру не удалось.",
                App.SingleInstanceDeliveryFailureText("ru"));
            Assert.AreEqual(
                "GCodeGenerator is already running, but the request could not be delivered.",
                App.SingleInstanceDeliveryFailureText("en"));
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

        /// <summary>
        /// У каждого кода проверки параметров есть перевод.
        ///
        /// Код объявляется в ядре, а текст к нему живёт в словаре приложения
        /// под ключом «Validation.&lt;код&gt;»; связывает их App при запуске.
        /// Новый код без перевода не ломает ни сборку, ни прогон: диалог
        /// молча показал бы английский текст для журнала, а найти это можно
        /// было бы только глазами в работающей программе.
        /// </summary>
        [TestMethod]
        public void EveryValidationCode_HasTranslatedMessage()
        {
            foreach (var culture in new[] { "ru", "en" })
            {
                var manager = CreateManager();
                manager.ChangeCulture(new CultureInfo(culture));

                foreach (Models.ValidationCode code in Enum.GetValues(typeof(Models.ValidationCode)))
                {
                    var key = "Validation." + code;
                    var text = manager.GetString(key);

                    Assert.IsFalse(string.IsNullOrWhiteSpace(text), key);
                    Assert.IsFalse(text.StartsWith("?", StringComparison.Ordinal),
                        $"{culture}: нет перевода для кода проверки {key}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Полнота перевода
        // ------------------------------------------------------------------

        /// <summary>
        /// Наборы строк совпадают ключ в ключ.
        ///
        /// Строка, добавленная в один файл и забытая в другом, ничего не
        /// ломает: русский сателлит молча отдаёт английскую строку из
        /// нейтрального набора, а забытая в нейтральном — показывается как
        /// «?ключ?». И то и другое видно только тому, кто откроет это окно
        /// на этом языке, а окна отказов открывают реже всего.
        /// </summary>
        [TestMethod]
        public void BothLanguages_HaveTheSameKeys()
        {
            var english = Keys(string.Empty);
            var russian = Keys(".ru");

            Assert.AreEqual(string.Empty, string.Join(", ", english.Except(russian).OrderBy(key => key)),
                "Нет перевода на русский");
            Assert.AreEqual(string.Empty, string.Join(", ", russian.Except(english).OrderBy(key => key)),
                "Нет английской строки");
        }

        /// <summary>
        /// Ни одна строка не пуста: пустое значение словарь возвращает так же,
        /// как отсутствующий ключ, — то есть заменяет строкой из нейтрального
        /// набора или «?ключом?».
        /// </summary>
        [TestMethod]
        public void NoTranslation_IsEmpty()
        {
            foreach (var culture in new[] { string.Empty, ".ru" })
            {
                foreach (Match entry in Entries(culture))
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(entry.Groups["text"].Value),
                        $"Пустая строка {entry.Groups["key"].Value} в наборе «{culture}»");
                }
            }
        }

        /// <summary>Ключи набора строк.</summary>
        /// <param name="culture">Суффикс файла: пусто — английский, «.ru» — русский.</param>
        private static HashSet<string> Keys(string culture)
            => new HashSet<string>(Entries(culture).Select(entry => entry.Groups["key"].Value));

        /// <summary>
        /// Записи набора строк — прямо из файла: собранный сателлит показал бы
        /// вместо пропуска английскую строку, и пропуск остался бы незаметен.
        /// </summary>
        /// <param name="culture">Суффикс файла: пусто — английский, «.ru» — русский.</param>
        private static MatchCollection Entries(string culture)
        {
            var text = File.ReadAllText(Path.Combine(
                RepositoryRootLocator.Find(), "GCodeGenerator", "Resources",
                $"LocalizableResources{culture}.resx"));

            return Regex.Matches(
                text,
                @"<data name=""(?<key>[^""]+)""[^>]*>\s*<value>(?<text>[^<]*)</value>");
        }
    }
}
