using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Документы репозитория: журнал изменений, правила участия, порядок
    /// сообщения об уязвимости, формы issue и английский README.
    ///
    /// Компилятор их не видит, поэтому устаревают они молча: ссылка ведёт
    /// в никуда, форма спрашивает журнал не по тому пути, английский README
    /// отстаёт от русского на раздел. Здесь закреплено то, потеря чего
    /// обходится дороже всего — связь документов друг с другом и с кодом.
    /// </summary>
    [TestClass]
    public class RepositoryDocsTests
    {
        private static string Root => RepositoryRootLocator.Find();

        private static string Read(params string[] parts)
            => File.ReadAllText(Path.Combine(Root, Path.Combine(parts)));

        /// <summary>
        /// Каждый документ на месте и не пуст. Пустой файл выглядит как
        /// заполненный: он есть в списке файлов и виден в интерфейсе GitHub.
        /// </summary>
        [TestMethod]
        [DataRow("CHANGELOG.md")]
        [DataRow("CONTRIBUTING.md")]
        [DataRow("SECURITY.md")]
        [DataRow("README.en.md")]
        [DataRow(".github/PULL_REQUEST_TEMPLATE.md")]
        [DataRow(".github/ISSUE_TEMPLATE/bug_report.yml")]
        [DataRow(".github/ISSUE_TEMPLATE/feature_request.yml")]
        [DataRow(".github/ISSUE_TEMPLATE/config.yml")]
        [DataRow("build/Get-ReleaseNotes.ps1")]
        public void Document_ExistsAndIsNotEmpty(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.IsTrue(File.Exists(path), $"нет файла {relativePath}");
            Assert.IsTrue(new FileInfo(path).Length > 200, $"файл {relativePath} пуст");
        }

        // ------------------------------------------------------------------
        // Журнал изменений и описание выпуска
        // ------------------------------------------------------------------

        /// <summary>
        /// В журнале есть раздел очередной версии, и он не пуст. Пустой
        /// раздел означает выпуск без описания — то самое, ради чего журнал
        /// и заводился.
        /// </summary>
        [TestMethod]
        public void Changelog_HasANonEmptySectionForTheNextRelease()
        {
            var section = ExtractSection(Read("CHANGELOG.md"), "Не выпущено");

            Assert.IsNotNull(section, "В журнале нет раздела «Не выпущено»");
            Assert.IsTrue(section.Contains("- "), "Раздел очередной версии пуст");
        }

        /// <summary>
        /// Скрипт достаёт раздел версии из журнала.
        ///
        /// Проверяется запуском, а не чтением: скрипт компилируется не
        /// компилятором C#, и его правка не ломает ни сборку, ни тесты —
        /// расхождение увидели бы только при выпуске, то есть в тот момент,
        /// когда чинить его дороже всего.
        /// </summary>
        [TestMethod]
        public void ReleaseNotesScript_ExtractsTheSectionOfOneVersion()
        {
            var changelog = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");
            File.WriteAllText(changelog, string.Join(Environment.NewLine, new[]
            {
                "# Журнал изменений",
                "",
                "## [1.2.3] - 2026-08-28",
                "",
                "### Исправлено",
                "",
                "- Строка про версию 1.2.3.",
                "",
                "## [1.2.2]",
                "",
                "- Строка про версию 1.2.2.",
                "",
                "## [2x0x0]",
                "",
                "- Строка про версию, которой нет.",
                ""
            }), new UTF8Encoding(false));

            try
            {
                var notes = RunReleaseNotes("1.2.3", changelog);

                StringAssert.Contains(notes, "Строка про версию 1.2.3.");
                Assert.IsFalse(notes.Contains("1.2.2"), "Соседний раздел в описание не попал");
                Assert.IsFalse(notes.StartsWith("\n") || notes.EndsWith("\n"),
                    "Пустые строки по краям обрезаны");

                // Точка в номере версии — не «любой символ»: иначе описание
                // одной версии досталось бы другой, у которой на месте точек
                // стоит что угодно.
                Assert.AreEqual(string.Empty, RunReleaseNotes("2.0.0", changelog).Trim(),
                    "Номер версии сверяется буквально, а не как выражение");

                // Раздела нет — скрипт молчит: собранный и проверенный выпуск
                // не должен срываться из-за незаполненного журнала.
                Assert.AreEqual(string.Empty, RunReleaseNotes("9.9.9", changelog).Trim());
            }
            finally
            {
                File.Delete(changelog);
            }
        }

        /// <summary>
        /// Рабочий процесс выпуска берёт описание из журнала, а список
        /// коммитов остаётся запасным вариантом. Без этой связи журнал —
        /// файл, который никто не читает.
        /// </summary>
        [TestMethod]
        public void ReleaseWorkflow_TakesItsDescriptionFromTheChangelog()
        {
            var workflow = Read(".github", "workflows", "release.yml");

            StringAssert.Contains(workflow, "build/Get-ReleaseNotes.ps1",
                "Рабочий процесс не вызывает скрипт описания выпуска");
            StringAssert.Contains(workflow, "body_path: ${{ steps.notes.outputs.path }}",
                "Описание выпуска не подставляется");
            Assert.IsFalse(Regex.IsMatch(workflow, @"generate_release_notes:\s*true\s*$", RegexOptions.Multiline),
                "Список коммитов выводится всегда, а должен — только без раздела в журнале");
        }

        // ------------------------------------------------------------------
        // Формы issue
        // ------------------------------------------------------------------

        /// <summary>
        /// Форма ошибки спрашивает журнал работы — и по тому самому пути,
        /// по которому программа его пишет. README просит приложить журнал,
        /// а форма — то место, где эту просьбу увидят.
        /// </summary>
        [TestMethod]
        public void BugReportForm_AsksForTheLogByItsRealPath()
        {
            var form = Read(".github", "ISSUE_TEMPLATE", "bug_report.yml");
            var logger = Read("GCodeGenerator", "Infrastructure", "FileAppLogger.cs");

            StringAssert.Contains(form, @"%LOCALAPPDATA%\GCodeGenerator\logs\gcodegenerator.log",
                "Форма не называет путь к журналу");
            StringAssert.Contains(logger, "gcodegenerator.log",
                "Имя файла журнала изменилось — обновите форму");
            StringAssert.Contains(logger, "logs",
                "Каталог журнала изменился — обновите форму");
        }

        /// <summary>
        /// Обе формы объявлены как формы GitHub: имя, описание и поля. Файл
        /// без них GitHub молча не покажет, и пользователь заведёт пустой
        /// issue — ровно то, от чего формы и заводились.
        /// </summary>
        [TestMethod]
        [DataRow("bug_report.yml")]
        [DataRow("feature_request.yml")]
        public void IssueForm_DeclaresNameDescriptionAndFields(string fileName)
        {
            var form = Read(".github", "ISSUE_TEMPLATE", fileName);

            foreach (var key in new[] { "name:", "description:", "body:", "validations:" })
                StringAssert.Contains(form, key, $"{fileName}: нет ключа {key}");

            Assert.IsTrue(Regex.IsMatch(form, @"^\s+required:\s*true", RegexOptions.Multiline),
                $"{fileName}: ни одно поле не обязательно — форму заполнят пустой");
        }

        /// <summary>
        /// Пустой issue завести нельзя, а путь для уязвимости назван отдельно:
        /// иначе о ней сообщат публично, до того как появится исправление.
        /// </summary>
        [TestMethod]
        public void IssueConfig_ClosesTheBlankFormAndNamesTheSecurityPath()
        {
            var config = Read(".github", "ISSUE_TEMPLATE", "config.yml");

            StringAssert.Contains(config, "blank_issues_enabled: false");
            StringAssert.Contains(config, "security/advisories/new",
                "Приватный путь для уязвимости не назван");
        }

        // ------------------------------------------------------------------
        // Связь документов
        // ------------------------------------------------------------------

        /// <summary>
        /// README ведёт к остальным документам: ими пользуются, только если
        /// о них узнали с первой страницы.
        /// </summary>
        [TestMethod]
        public void Readme_LinksToTheOtherDocuments()
        {
            var readme = Read("README.md");

            foreach (var target in new[] { "README.en.md", "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md" })
                StringAssert.Contains(readme, target, $"README не ссылается на {target}");
        }

        /// <summary>
        /// Английский README не отстаёт от русского по составу: одинаковые
        /// разделы и одинаковая вложенность. Перевод устаревает разделом,
        /// добавленным только в один из двух файлов, — и заметить это может
        /// лишь тот, кто читает оба.
        /// </summary>
        [TestMethod]
        public void BothReadmes_HaveTheSameSectionStructure()
        {
            var russian = HeadingLevels(Read("README.md"));
            var english = HeadingLevels(Read("README.en.md"));

            Assert.AreEqual(
                string.Join(" ", russian),
                string.Join(" ", english),
                "Состав разделов README.md и README.en.md разошёлся");
            Assert.IsTrue(russian.Count > 15, "Разделов подозрительно мало — проверьте разбор");
        }

        /// <summary>
        /// Английский README ведёт к тем же документам и говорит, что issue
        /// можно писать по-английски: формы у репозитория русские, и без этой
        /// строки англоязычный читатель решит, что писать нужно по-русски.
        /// </summary>
        [TestMethod]
        public void EnglishReadme_LinksBackAndInvitesEnglishIssues()
        {
            var readme = Read("README.en.md");

            StringAssert.Contains(readme, "(README.md)", "Нет ссылки на русскую версию");
            foreach (var target in new[] { "CONTRIBUTING.md", "SECURITY.md", "CHANGELOG.md" })
                StringAssert.Contains(readme, target, $"README.en.md не ссылается на {target}");
            StringAssert.Contains(readme, "in English");
        }

        /// <summary>
        /// Правила участия ведут к порядку сообщения об уязвимости и к журналу
        /// изменений: это две вещи, о которых участник узнаёт слишком поздно.
        /// </summary>
        [TestMethod]
        public void Contributing_PointsToSecurityAndChangelog()
        {
            var contributing = Read("CONTRIBUTING.md");

            StringAssert.Contains(contributing, "SECURITY.md");
            StringAssert.Contains(contributing, "CHANGELOG.md");
            StringAssert.Contains(contributing, "GCG_WRITE_GOLDEN",
                "Не сказано, как обновлять эталонные файлы");
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        /// <summary>Уровни заголовков документа сверху вниз: «# ## ### ##».</summary>
        private static System.Collections.Generic.List<string> HeadingLevels(string markdown)
        {
            var inCodeBlock = false;
            var levels = new System.Collections.Generic.List<string>();

            foreach (var line in markdown.Split('\n'))
            {
                var text = line.TrimEnd('\r');
                if (text.StartsWith("```", StringComparison.Ordinal))
                {
                    inCodeBlock = !inCodeBlock;
                    continue;
                }

                if (inCodeBlock)
                    continue;

                var match = Regex.Match(text, @"^(#{1,6})\s");
                if (match.Success)
                    levels.Add(match.Groups[1].Value);
            }

            return levels;
        }

        /// <summary>Раздел журнала изменений по имени версии; null — раздела нет.</summary>
        private static string ExtractSection(string changelog, string version)
        {
            var match = Regex.Match(
                changelog,
                @"^##\s+\[" + Regex.Escape(version) + @"\][^\n]*\n(?<body>.*?)(?=^##\s|\z)",
                RegexOptions.Multiline | RegexOptions.Singleline);

            return match.Success ? match.Groups["body"].Value.Trim() : null;
        }

        /// <summary>
        /// Запускает скрипт описания выпуска и возвращает то, что он записал.
        ///
        /// Результат читается из файла, а не из вывода: файл — это и есть то,
        /// чем пользуется рабочий процесс, и записан он в UTF-8, тогда как
        /// вывод консоли Windows PowerShell идёт в кодовой странице консоли.
        /// Файла нет — раздела для этой версии в журнале не нашлось.
        /// </summary>
        /// <param name="tag">Версия, раздел которой нужен.</param>
        /// <param name="changelogPath">Журнал изменений.</param>
        private static string RunReleaseNotes(string tag, string changelogPath)
        {
            var script = Path.Combine(Root, "build", "Get-ReleaseNotes.ps1");
            var outFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".md");
            var start = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in new[]
                     {
                         "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                         "-File", script, "-Tag", tag, "-Path", changelogPath, "-OutFile", outFile
                     })
            {
                start.ArgumentList.Add(argument);
            }

            try
            {
                using var process = Process.Start(start)!;
                process.StandardOutput.ReadToEnd();
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);

                Assert.AreEqual(0, process.ExitCode,
                    $"Скрипт завершился с кодом {process.ExitCode}: {errors}");

                if (!File.Exists(outFile))
                    return string.Empty;

                var bytes = File.ReadAllBytes(outFile);
                Assert.IsFalse(bytes.Length > 2 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "Метка порядка байтов попала бы в описание выпуска отдельным символом");

                return new UTF8Encoding(false).GetString(bytes).Replace("\r\n", "\n").Trim('\n');
            }
            finally
            {
                if (File.Exists(outFile))
                    File.Delete(outFile);
            }
        }
    }
}
