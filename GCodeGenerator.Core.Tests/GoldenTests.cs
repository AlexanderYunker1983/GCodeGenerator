using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Золотые тесты генератора (пункт 0.4 плана): для каждой фикстуры
    /// <see cref="FixtureCatalog.All"/> результат <c>SimpleGCodeGenerator.Generate(...)</c>
    /// сравнивается построчно с golden-файлом <c>Golden/&lt;имя фикстуры&gt;.nc</c>.
    ///
    /// Культура прогона не закрепляется: вывод инвариантен по построению —
    /// координаты форматирует GCodeGenerationHelper, описания операций собирает
    /// OperationBase.Invariant, — поэтому прогон под любой локалью обязан
    /// совпадать с эталонами. Раньше культура закреплялась здесь вручную
    /// и прятала зависимость описаний операций от локали машины.
    ///
    /// Обновление golden-файлов: установить переменную окружения GCG_WRITE_GOLDEN=1
    /// и выполнить тест <see cref="Write_Golden_Files"/> (пишет в исходный каталог),
    /// затем пересобрать, проверить diff и закоммитить файлы.
    /// </summary>
    [TestClass]
    public class GoldenTests
    {
        private static readonly SimpleGCodeGenerator Generator = new SimpleGCodeGenerator();

        public TestContext TestContext { get; set; }

        /// <summary>Golden-файлы в каталоге сборки тестов (копия из исходников).</summary>
        private static string GoldenOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Golden");

        /// <summary>Golden-файлы в исходном каталоге — единственный источник истины.</summary>
        private static string GoldenSourceDirectory =>
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Golden"));

        [TestMethod]
        public void Golden_Programs_Match_Golden_Files()
        {
            var failures = new List<string>();
            var checkedCount = 0;

            foreach (var fixture in FixtureCatalog.All)
            {
                checkedCount++;
                var program = Generator.Generate(fixture.Operations, fixture.Settings);
                var failure = CompareWithGolden(fixture.Name, program.Lines);
                if (failure != null)
                    failures.Add(failure);
            }

            Assert.AreEqual(FixtureCatalog.All.Count, checkedCount, "Каталог фикстур не пуст");
            if (failures.Count > 0)
                Assert.Fail($"Golden-несоответствий: {failures.Count}\n\n{string.Join("\n\n", failures)}");
        }

        /// <summary>
        /// Обратная сторона соответствия: удалённая или переименованная
        /// фикстура не должна оставлять в репозитории мёртвый эталон.
        /// </summary>
        [TestMethod]
        public void Golden_SourceDirectory_HasNoOrphanedPrograms()
        {
            var fixtureFiles = FixtureCatalog.All
                .Select(fixture => fixture.Name + ".nc")
                .ToHashSet(StringComparer.Ordinal);
            var orphanedFiles = Directory
                .EnumerateFiles(GoldenSourceDirectory, "*.nc", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(fileName => !fixtureFiles.Contains(fileName))
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(0, orphanedFiles.Length,
                "Golden-каталог содержит файлы без действующей фикстуры: "
                + string.Join(", ", orphanedFiles));
        }

        /// <summary>
        /// Перегенерация golden-файлов в исходный каталог.
        /// Выполняется только при GCG_WRITE_GOLDEN=1 (в CI — no-op).
        /// </summary>
        [TestMethod]
        public void Write_Golden_Files()
        {
            if (Environment.GetEnvironmentVariable("GCG_WRITE_GOLDEN") != "1")
                return; // no-op вне ручного режима; в CI golden-файлы только читаются

            Directory.CreateDirectory(GoldenSourceDirectory);
            foreach (var fixture in FixtureCatalog.All)
            {
                var program = Generator.Generate(fixture.Operations, fixture.Settings);
                var path = Path.Combine(GoldenSourceDirectory, fixture.Name + ".nc");
                File.WriteAllLines(path, program.Lines);
            }
        }

        /// <summary>Сравнение программы с golden-файлом; null — совпадает.</summary>
        private string CompareWithGolden(string fixtureName, IList<string> actualLines)
        {
            var actual = actualLines.ToList();
            var goldenPath = Path.Combine(GoldenOutputDirectory, fixtureName + ".nc");

            if (!File.Exists(goldenPath))
                return $"{fixtureName}: нет golden-файла {fixtureName}.nc " +
                       "(запустите Write_Golden_Files с GCG_WRITE_GOLDEN=1 и закоммитьте файлы)";

            var expected = File.ReadAllLines(goldenPath).ToList();
            if (expected.SequenceEqual(actual))
                return null;

            var sb = new StringBuilder();
            sb.AppendLine($"{fixtureName}: golden-несоответствие (ожидалось {expected.Count} строк, фактически {actual.Count}):");
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
