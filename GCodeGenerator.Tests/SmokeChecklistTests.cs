using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Ручной прогон описывает продукт, который существует.
    ///
    /// Прежний чек-лист устарел молча и целиком: он предупреждал, что работает
    /// только одна стратегия выборки, что настройки шпинделя не попадают в файл
    /// проекта, что разбор чертежей теряет вершины полилиний, — всё это давно
    /// исправлено. Половина ожидаемых результатов была заведомо неверной, то
    /// есть выполнить его перед выпуском было нельзя. Ничто его с продуктом
    /// не связывало, поэтому и разошлись они незаметно.
    ///
    /// Здесь связаны те утверждения чек-листа, которые можно сверить с кодом:
    /// число диалогов, названия автотестов, на которые он ссылается, и файлы
    /// чертежей, которые предлагает открыть.
    /// </summary>
    [TestClass]
    public class SmokeChecklistTests
    {
        private static string Root => RepositoryRootLocator.Find();

        private static string Checklist
            => File.ReadAllText(Path.Combine(Root, "docs", "SMOKE_CHECKLIST.md"));

        /// <summary>
        /// Число диалогов в чек-листе совпадает с каталогом операций: диалог
        /// есть у каждого типа, а у сверления — у каждого режима расстановки.
        /// </summary>
        [TestMethod]
        public void Checklist_CountsTheDialogsThatExist()
        {
            var drillDialogs = OperationEditorRegistry.DrillRegistrations.Count;
            var otherDialogs = OperationEditorRegistry.Registrations.Count;
            var profiles = OperationCatalog.ByCategory(OperationCategory.Profile).Count();
            var pockets = OperationCatalog.ByCategory(OperationCategory.Pocket).Count();

            var checklist = Checklist;

            StringAssert.Contains(checklist, $"Все {drillDialogs + otherDialogs} диалогов",
                "Общее число диалогов в чек-листе разошлось с каталогом");
            StringAssert.Contains(checklist, $"{drillDialogs} видов сверления",
                "Число режимов сверления в чек-листе разошлось с каталогом");
            StringAssert.Contains(checklist, $"{profiles} профилей",
                "Число профильных операций в чек-листе разошлось с каталогом");
            StringAssert.Contains(checklist, $"{pockets} кармана",
                "Число карманов в чек-листе разошлось с каталогом");
        }

        /// <summary>
        /// Чек-лист перечисляет все стратегии выборки поимённо: прежний
        /// утверждал, что работает одна из них.
        /// </summary>
        [TestMethod]
        public void Checklist_MentionsEveryPocketStrategy()
        {
            var checklist = Checklist;
            var names = new Dictionary<PocketStrategy, string>
            {
                [PocketStrategy.Spiral] = "спираль",
                [PocketStrategy.Concentric] = "концентрические",
                [PocketStrategy.Radial] = "радиальные",
                [PocketStrategy.ZigZag] = "зигзаг",
                [PocketStrategy.Lines] = "линии",
            };

            foreach (PocketStrategy strategy in Enum.GetValues(typeof(PocketStrategy)))
            {
                Assert.IsTrue(names.ContainsKey(strategy),
                    $"Стратегия {strategy} появилась в продукте, но не названа в этой проверке");
                StringAssert.Contains(checklist.ToLowerInvariant(), names[strategy],
                    $"Чек-лист не упоминает стратегию {strategy}");
            }
        }

        /// <summary>
        /// Названия автотестов, на которые ссылается чек-лист, существуют.
        /// Ссылка на исчезнувший тест — обещание проверки, которой нет.
        /// </summary>
        [TestMethod]
        public void Checklist_ReferencesExistingTests()
        {
            var referenced = Regex.Matches(Checklist, @"`(?<name>[A-Za-z]*\*?[A-Za-z]*Tests)`")
                .Select(match => match.Groups["name"].Value)
                .Distinct()
                .ToList();

            Assert.IsTrue(referenced.Count > 0, "Чек-лист не ссылается ни на один автотест");

            var testFiles = new[] { "GCodeGenerator.Tests", "GCodeGenerator.Core.Tests" }
                .SelectMany(project => Directory.EnumerateFiles(
                    Path.Combine(Root, project), "*Tests.cs", SearchOption.AllDirectories))
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();

            var missing = referenced
                .Where(name => !testFiles.Any(file => Matches(name, file!)))
                .ToList();

            Assert.AreEqual(0, missing.Count,
                "Чек-лист ссылается на несуществующие тесты: " + string.Join(", ", missing));
        }

        /// <summary>Имя из чек-листа может содержать звёздочку: «Dxf*Tests».</summary>
        private static bool Matches(string reference, string fileName)
        {
            if (!reference.Contains('*'))
                return string.Equals(reference, fileName, StringComparison.Ordinal);

            var pattern = "^" + string.Join(".*", reference.Split('*').Select(Regex.Escape)) + "$";
            return Regex.IsMatch(fileName, pattern);
        }

        /// <summary>
        /// Чертежи, которые чек-лист предлагает открыть, лежат на месте.
        /// </summary>
        [TestMethod]
        public void Checklist_ReferencesExistingAssets()
        {
            var assets = Regex.Matches(Checklist, @"`(?<name>[a-z_]+\.dxf)`")
                .Select(match => match.Groups["name"].Value)
                .Distinct()
                .ToList();

            Assert.IsTrue(assets.Count > 0, "Чек-лист не называет ни одного чертежа");

            foreach (var asset in assets)
            {
                var path = Path.Combine(Root, "GCodeGenerator.Core.Tests", "Assets", asset);
                Assert.IsTrue(File.Exists(path), $"Нет чертежа, названного в чек-листе: {asset}");
            }
        }

        /// <summary>
        /// В чек-листе не осталось следов плана рефакторинга: ни ссылок на
        /// документ, которого нет в репозитории, ни разделов о поведении,
        /// которое давно исправлено.
        /// </summary>
        [TestMethod]
        public void Checklist_HasNoLeftoversOfTheRefactoringPlan()
        {
            var leftovers = new[] { "Plan.md", "фаза 0", "фазы 1", "as-is", "As-is", "п. 0." };
            var checklist = Checklist;

            var found = leftovers.Where(text => checklist.Contains(text, StringComparison.Ordinal)).ToList();

            Assert.AreEqual(0, found.Count,
                "Чек-лист ссылается на план рефакторинга: " + string.Join(", ", found));
        }

        /// <summary>
        /// Ручной прогон проверяет текущую область действия клавиш и политику
        /// ASCII-комментариев. Эти свойства видны только в живом интерфейсе и
        /// готовом файле, поэтому одной XAML- или unit-проверки недостаточно.
        /// </summary>
        [TestMethod]
        public void Checklist_CoversCurrentKeyboardAndCommentPolicies()
        {
            var checklist = Checklist;

            StringAssert.Contains(checklist, "Ctrl+G");
            StringAssert.Contains(checklist, "Delete удаляет выделенную операцию");
            Assert.IsTrue(Regex.IsMatch(checklist, @"русское\s+имя в файл не попадает"),
                "Чек-лист не проверяет исключение русского имени из G-code");
            StringAssert.Contains(checklist, "проверяется вместе с L8");
            Assert.IsFalse(checklist.Contains(
                    "Переименование операции сохраняется и видно в комментарии G-кода",
                    StringComparison.Ordinal),
                "Чек-лист требует устаревший вывод любого имени операции в G-code");
        }
    }
}
