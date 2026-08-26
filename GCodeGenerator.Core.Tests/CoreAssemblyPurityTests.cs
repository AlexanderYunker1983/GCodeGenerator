using System;
using System.IO;
using System.Linq;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Ядро не зависит от WPF: оно собирается и тестируется под чистый
    /// net10.0, и программа для станка не должна тянуть за собой оконный
    /// стек. Прежде это правило жило PowerShell-регэкспом в CI — локально
    /// оно не выполнялось, а полное имя типа вместо using его обходило.
    /// Проверка по собранной сборке видит фактические ссылки: обход возможен
    /// только не используя WPF вовсе, что и требуется.
    /// </summary>
    [TestClass]
    public class CoreAssemblyPurityTests
    {
        private static readonly string[] WpfAssemblies =
        {
            "PresentationCore", "PresentationFramework", "WindowsBase", "System.Xaml",
        };

        [TestMethod]
        public void CoreAssembly_DoesNotReferenceWpf()
        {
            var references = typeof(OperationBase).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToList();

            Assert.IsTrue(references.Count > 0, "Список ссылок пуст — проверка ничего не проверяет");

            var offenders = references
                .Where(name => WpfAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase))
                .ToList();
            Assert.AreEqual(0, offenders.Count,
                "Ядро ссылается на WPF: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// UseWPF в проекте ядра дал бы компилятору доступ к оконному стеку —
        /// и первый же WPF-тип в ядре прошёл бы сборку. Файл проекта
        /// проверяется отдельно от ссылок сборки: свойство опасно само по
        /// себе, ещё до первого использования.
        /// </summary>
        [TestMethod]
        public void CoreProject_DoesNotEnableWpf()
        {
            var projectPath = Path.Combine(
                RepositoryRoot.Find(), "GCodeGenerator.Core", "GCodeGenerator.Core.csproj");
            var project = File.ReadAllText(projectPath);

            StringAssert.DoesNotMatch(project, new System.Text.RegularExpressions.Regex("<UseWPF>\\s*true", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                "UseWPF включён в проекте ядра");
            StringAssert.DoesNotMatch(project, new System.Text.RegularExpressions.Regex("<UseWindowsForms>\\s*true", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                "UseWindowsForms включён в проекте ядра");
        }
    }

    /// <summary>
    /// Корень репозитория для проверок, читающих исходники: от каталога
    /// тестовой сборки вверх до файла решения.
    /// </summary>
    internal static class RepositoryRoot
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
