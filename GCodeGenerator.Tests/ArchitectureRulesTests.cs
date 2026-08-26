using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Архитектурные правила по исходникам. Прежде они жили
    /// PowerShell-регэкспами в CI-конвейере: локально не выполнялись, и
    /// нарушение обнаруживалось только после отправки. Здесь те же правила —
    /// обычные тесты, одинаковые на машине разработчика и в CI.
    ///
    /// Комментарии из проверок исключены: правила ограничивают код, а слова —
    /// нет (упоминание Dispatcher в пояснении нарушением не является).
    /// </summary>
    [TestClass]
    public class ArchitectureRulesTests
    {
        /// <summary>Каталоги продуктовых исходников (не тестов).</summary>
        private static readonly string[] ProductProjects = { "GCodeGenerator.Core", "GCodeGenerator" };

        /// <summary>
        /// Сгенерированные файлы: их пишет инструмент, директиву они не
        /// получают, и править их руками нельзя.
        /// </summary>
        private static bool IsGenerated(string path)
            => path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
               || path.EndsWith(Path.Combine("Properties", "AssemblyInfo.cs"), StringComparison.OrdinalIgnoreCase);

        private static string Root => GCodeGenerator.Tests.RepositoryRootLocator.Find();

        private static IEnumerable<string> SourceFiles(string relativeDirectory)
            => Directory.EnumerateFiles(Path.Combine(Root, relativeDirectory), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                               && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

        /// <summary>Строки кода файла: без целострочных комментариев.</summary>
        private static IEnumerable<(int Number, string Text)> CodeLines(string path)
            => File.ReadAllLines(path)
                .Select((text, index) => (Number: index + 1, Text: text))
                .Where(line => !Regex.IsMatch(line.Text, @"^\s*//"));

        private static void AssertNoMatches(string relativeDirectory, Regex rule, string message)
        {
            var violations = new List<string>();
            foreach (var path in SourceFiles(relativeDirectory))
            {
                foreach (var (number, text) in CodeLines(path))
                {
                    if (rule.IsMatch(text))
                        violations.Add($"{Path.GetFileName(path)}:{number}: {text.Trim()}");
                }
            }

            Assert.AreEqual(0, violations.Count,
                message + Environment.NewLine + string.Join(Environment.NewLine, violations));
        }

        /// <summary>
        /// Проверка ссылок на пустоту включается пофайлово, и новый файл
        /// не получает её сам: забытая директива — это файл, в котором
        /// проверка молча не работает. Заявление «директива стоит во всех
        /// файлах продукта» из Directory.Build.props держится этим тестом.
        /// </summary>
        [TestMethod]
        public void EveryProductSourceFile_EnablesNullable()
        {
            var missing = new List<string>();
            var checkedFiles = 0;

            foreach (var project in ProductProjects)
            {
                foreach (var path in SourceFiles(project))
                {
                    if (IsGenerated(path))
                        continue;

                    checkedFiles++;
                    var lines = File.ReadAllLines(path);
                    var directiveIndex = Array.FindIndex(lines, line => line.TrimStart().StartsWith("#nullable enable", StringComparison.Ordinal));
                    var codeIndex = Array.FindIndex(lines, line =>
                    {
                        var trimmed = line.TrimStart();
                        return trimmed.StartsWith("using ", StringComparison.Ordinal)
                               || trimmed.StartsWith("namespace ", StringComparison.Ordinal);
                    });

                    // Директива обязана предшествовать коду: поставленная в
                    // конце файла, она бы «включала» проверку для пустоты.
                    if (directiveIndex < 0 || (codeIndex >= 0 && directiveIndex > codeIndex))
                        missing.Add(Path.GetRelativePath(Root, path));
                }
            }

            Assert.IsTrue(checkedFiles > 200, $"проверено {checkedFiles} файлов — проверка ничего не проверяет");
            Assert.AreEqual(0, missing.Count,
                "Файлы без #nullable enable до первого кода:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
        }

        /// <summary>
        /// View-модели не трогают оконный стек: System.Windows.Input
        /// (ICommand) — контракт MVVM и разрешён, остальной WPF — нет.
        /// Полное имя типа без using правило тоже не обходит.
        /// </summary>
        [TestMethod]
        public void ViewModels_DoNotTouchWpfUi()
        {
            AssertNoMatches(
                Path.Combine("GCodeGenerator", "ViewModels"),
                new Regex(@"^\s*using\s+(System\.Windows(;|\.(?!Input\b))|System\.Xaml|PresentationCore|WindowsBase|PresentationFramework)|System\.Windows\.(?!Input\b)\w|Application\.Current|\bDispatcher\b"),
                "WPF в view-моделях:");
        }

        /// <summary>
        /// View-модели координируют сервисы, но сами не читают и не пишут
        /// файлы и не сериализуют данные: этим заняты службы за интерфейсами.
        /// </summary>
        [TestMethod]
        public void ViewModels_DoNotDoFileIoOrSerialization()
        {
            AssertNoMatches(
                Path.Combine("GCodeGenerator", "ViewModels"),
                new Regex(@"^\s*using\s+System\.IO\b|\bSystem\.IO\b|\b(File|Directory|FileInfo|DirectoryInfo|StreamReader|StreamWriter)\s*\.|\bJsonSerializer\b"),
                "Файловый ввод-вывод или сериализация в view-моделях:");
        }

        /// <summary>
        /// Конкретные службы приходят через IoC, а не создаются на месте:
        /// созданную вручную службу нельзя подменить в тестах.
        /// </summary>
        [TestMethod]
        public void ViewModels_DoNotConstructServices()
        {
            AssertNoMatches(
                Path.Combine("GCodeGenerator", "ViewModels"),
                new Regex(@"\bnew\s+\w+Service\s*\("),
                "Службы, созданные в view-моделях:");
        }

        /// <summary>
        /// Пункт 8.3 плана: в view-моделях нет захардкоженного текста —
        /// кириллица в строковых литералах запрещена, текст приходит только
        /// по ключам словаря локализации.
        /// </summary>
        [TestMethod]
        public void ViewModels_HaveNoCyrillicStringLiterals()
        {
            AssertNoMatches(
                Path.Combine("GCodeGenerator", "ViewModels"),
                new Regex("\"[^\"]*[Ѐ-ӿ][^\"]*\""),
                "Кириллица в строковых литералах view-моделей:");
        }
    }

    /// <summary>
    /// Корень репозитория для проверок, читающих исходники: от каталога
    /// тестовой сборки вверх до файла решения.
    /// </summary>
    internal static class RepositoryRootLocator
    {
        public static string Find()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "GCodeGenerator.sln")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new InvalidOperationException("GCodeGenerator.sln not found above the test directory.");
        }
    }
}
