using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Дифференциальный тест (пункт 4.3 плана): для всех фикстур старое
    /// (строки) == новое (структура → форматтер) построчно.
    ///
    /// «Старое» зафиксировано в golden-файлах (пункт 0.4) и эталонном
    /// reference_project.nc (пункт 0.7) — побайтовый вывод прежнего
    /// строкового пайплайна. «Новое» — текущий пайплайн: ProgramBuilder
    /// (структура) → GCodeFormatter (рендеринг).
    ///
    /// Дополнительно проверяется, что вывод действительно прошёл через
    /// структуру: число блоков == числу строк, блоки не пусты.
    /// Переключение пайплайна (4.4) выполняется только при 100% равенстве.
    /// </summary>
    [TestClass]
    public class DifferentialTests
    {
        private static readonly SimpleGCodeGenerator Generator = new SimpleGCodeGenerator();
        private static readonly ProjectFileService Service = new ProjectFileService();
        private static CultureInfo _originalCulture;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        private static string GoldenOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Golden");

        private static string ReferenceOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference");

        /// <summary>
        /// Все 31 фикстура: новый пайплайн (структура → форматтер) == golden
        /// (старый строковый вывод) построчно.
        /// </summary>
        [TestMethod]
        public void All_Fixtures_StructuredOutput_Equals_LegacyGolden()
        {
            var failures = new List<string>();

            foreach (var fixture in FixtureCatalog.All)
            {
                var program = Generator.Generate(fixture.Operations, fixture.Settings);

                // Вывод прошёл через структуру: блоки == строки, блоки не пусты.
                if (program.Blocks.Count != program.Lines.Count || program.Blocks.Count == 0)
                {
                    failures.Add($"{fixture.Name}: программа не прошла через структуру " +
                                 $"(Blocks={program.Blocks.Count}, Lines={program.Lines.Count})");
                    continue;
                }

                var goldenPath = Path.Combine(GoldenOutputDirectory, fixture.Name + ".nc");
                if (!File.Exists(goldenPath))
                {
                    failures.Add($"{fixture.Name}: нет golden-файла {fixture.Name}.nc");
                    continue;
                }

                var expected = File.ReadAllLines(goldenPath).ToList();
                var actual = program.Lines.ToList();
                if (!expected.SequenceEqual(actual))
                    failures.Add(Diff(fixture.Name, expected, actual));
            }

            if (failures.Count > 0)
                Assert.Fail($"Дифференциальных несоответствий: {failures.Count}\n\n{string.Join("\n\n", failures)}");
        }

        /// <summary>
        /// Эталонный проект (19 операций, полный пайплайн через .ygc): новый
        /// пайплайн == reference_project.nc построчно.
        /// </summary>
        [TestMethod]
        public void ReferenceProject_StructuredOutput_Equals_LegacyNc()
        {
            var ygcPath = Path.Combine(ReferenceOutputDirectory, "reference_project.ygc");
            Assert.IsTrue(File.Exists(ygcPath), "Нет эталонного проекта reference_project.ygc");

            var operations = Service.Load(ygcPath);
            var program = Generator.Generate(operations, new GCodeSettings());

            if (program.Blocks.Count != program.Lines.Count || program.Blocks.Count == 0)
                Assert.Fail($"Эталонный проект не прошёл через структуру " +
                            $"(Blocks={program.Blocks.Count}, Lines={program.Lines.Count})");

            var ncPath = Path.Combine(ReferenceOutputDirectory, "reference_project.nc");
            var expected = File.ReadAllLines(ncPath).ToList();
            var actual = program.Lines.ToList();
            if (!expected.SequenceEqual(actual))
                Assert.Fail(Diff("reference_project", expected, actual));
        }

        /// <summary>Первые расхождения построчно (до 10).</summary>
        private static string Diff(string name, List<string> expected, List<string> actual)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{name}: несоответствие (ожидалось {expected.Count} строк, фактически {actual.Count}):");
            var shown = 0;
            for (int i = 0; i < Math.Max(expected.Count, actual.Count) && shown < 10; i++)
            {
                var e = i < expected.Count ? expected[i] : "<нет строки>";
                var a = i < actual.Count ? actual[i] : "<нет строки>";
                if (e != a)
                {
                    sb.AppendLine($"  строка {i + 1}: ожидается \"{e}\", фактически \"{a}\"");
                    shown++;
                }
            }
            return sb.ToString();
        }
    }
}
