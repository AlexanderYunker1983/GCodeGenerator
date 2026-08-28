using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Слежение за уязвимостями в дереве пакетов.
    ///
    /// Уязвимость появляется не в момент правки кода, а в момент публикации
    /// записи в базе: тот же самый коммит вчера был чистым, а сегодня нет.
    /// Заметить это может только проверка, идущая на каждой сборке, и здесь
    /// закреплено то, что делает её проверкой, а не украшением: разбор
    /// находки, отказ на ней и невозможность пройти молча, когда сама
    /// проверка перестала работать.
    /// </summary>
    [TestClass]
    public class DependencyAuditTests
    {
        private static string Root => RepositoryRootLocator.Find();

        private static string Read(params string[] parts)
            => File.ReadAllText(Path.Combine(Root, Path.Combine(parts)));

        // ------------------------------------------------------------------
        // Скрипт проверки
        // ------------------------------------------------------------------

        /// <summary>
        /// Находка — отказ, и в отчёте видно, что именно найдено: пакет,
        /// степень опасности и ссылка на описание. Без них сообщение «есть
        /// уязвимость» отправляет искать её заново.
        /// </summary>
        [TestMethod]
        public void Script_FailsOnAFindingAndNamesIt()
        {
            var listing = Listing(@"
            {
              ""version"": 1,
              ""projects"": [
                { ""path"": ""C:/repo/Clean.csproj"" },
                {
                  ""path"": ""C:/repo/App.csproj"",
                  ""frameworks"": [
                    {
                      ""framework"": ""net10.0-windows"",
                      ""topLevelPackages"": [
                        { ""id"": ""Some.Direct"", ""resolvedVersion"": ""1.0.0"",
                          ""vulnerabilities"": [ { ""severity"": ""High"",
                            ""advisoryurl"": ""https://github.com/advisories/GHSA-1111"" } ] }
                      ],
                      ""transitivePackages"": [
                        { ""id"": ""Some.Transitive"", ""resolvedVersion"": ""2.0.0"",
                          ""vulnerabilities"": [ { ""severity"": ""Critical"",
                            ""advisoryurl"": ""https://github.com/advisories/GHSA-2222"" } ] }
                      ]
                    }
                  ]
                }
              ]
            }");

            try
            {
                var (exitCode, output) = RunScript(listing);

                Assert.AreEqual(1, exitCode, "Находка должна ронять прогон");
                StringAssert.Contains(output, "Some.Direct 1.0.0");
                StringAssert.Contains(output, "High");
                StringAssert.Contains(output, "https://github.com/advisories/GHSA-1111");

                // Транзитивный пакет — главное, ради чего проверка и нужна:
                // его версию проект не называет и сам не выбирает.
                StringAssert.Contains(output, "Some.Transitive 2.0.0");
                StringAssert.Contains(output, "Critical");
            }
            finally
            {
                File.Delete(listing);
            }
        }

        /// <summary>Чистое дерево проходит: проверка не мешает обычной сборке.</summary>
        [TestMethod]
        public void Script_PassesOnACleanTree()
        {
            var listing = Listing(@"
            {
              ""version"": 1,
              ""projects"": [
                { ""path"": ""C:/repo/App.csproj"" },
                { ""path"": ""C:/repo/Core.csproj"" }
              ]
            }");

            try
            {
                var (exitCode, output) = RunScript(listing);

                Assert.AreEqual(0, exitCode, output);
                StringAssert.Contains(output, "No known vulnerable packages");
            }
            finally
            {
                File.Delete(listing);
            }
        }

        /// <summary>
        /// Сломанный или пустой ответ — отказ, а не «ничего не найдено».
        /// Проверка, которая зеленеет ровно тогда, когда перестала работать,
        /// хуже её отсутствия: на неё рассчитывают.
        /// </summary>
        [TestMethod]
        [DataRow(@"{ ""version"": 1, ""projects"": [] }", DisplayName = "проектов нет")]
        [DataRow("не JSON вовсе", DisplayName = "ответ не разбирается")]
        public void Script_FailsWhenNothingWasActuallyChecked(string content)
        {
            var listing = Listing(content);

            try
            {
                var (exitCode, output) = RunScript(listing);

                Assert.AreEqual(1, exitCode, output);
                StringAssert.Contains(output, "ERROR:");
            }
            finally
            {
                File.Delete(listing);
            }
        }

        /// <summary>
        /// Разбирается машинный вывод, а не человеческий: человеческий
        /// переведён, и проверка, ищущая в нём английскую фразу, перестаёт
        /// работать на машине с другим языком — молча и в зелёную сторону.
        /// </summary>
        [TestMethod]
        public void Script_ReadsTheMachineReadableListing()
        {
            var script = Read("build", "Test-VulnerablePackages.ps1");

            StringAssert.Contains(script, "--format json");
            StringAssert.Contains(script, "--include-transitive");
        }

        // ------------------------------------------------------------------
        // Рабочий процесс и настройки восстановления
        // ------------------------------------------------------------------

        /// <summary>
        /// Проверка вызывается на каждой сборке и выполняется даже после
        /// упавшего теста: уязвимость не перестаёт существовать оттого,
        /// что что-то другое сломалось.
        /// </summary>
        [TestMethod]
        public void CiWorkflow_RunsTheCheckAlways()
        {
            var workflow = Read(".github", "workflows", "ci.yml");

            StringAssert.Contains(workflow, "build/Test-VulnerablePackages.ps1",
                "CI не вызывает проверку уязвимостей");

            var step = Regex.Match(
                workflow,
                @"- name: Check for vulnerable packages(?<body>.*?)(?=\n      - name:|\z)",
                RegexOptions.Singleline);

            Assert.IsTrue(step.Success, "Шага проверки в рабочем процессе нет");
            StringAssert.Contains(step.Groups["body"].Value, "if: always()",
                "Проверка пропускается после упавшего теста");
        }

        /// <summary>
        /// Восстановление сверяет с базой уязвимостей всё дерево, а не только
        /// прямые ссылки, и делает это по явной настройке: умолчание менялось
        /// от версии пакета разработки к версии.
        /// </summary>
        [TestMethod]
        public void Restore_AuditsTheWholeTree()
        {
            var props = Read("Directory.Build.props");

            StringAssert.Contains(props, "<NuGetAudit>true</NuGetAudit>");
            StringAssert.Contains(props, "<NuGetAuditMode>all</NuGetAuditMode>");
        }

        /// <summary>
        /// Предупреждения об уязвимостях не превращаются в ошибку
        /// восстановления. Иначе новая запись в базе ломала бы первое же
        /// действие сборки коротким сообщением с кодом — вместо отчёта,
        /// который называет пакет и что с ним делать.
        /// </summary>
        [TestMethod]
        public void AuditWarnings_DoNotBreakTheRestore()
        {
            var props = Read("Directory.Build.props");

            foreach (var code in new[] { "NU1901", "NU1902", "NU1903", "NU1904" })
                StringAssert.Contains(props, code, $"{code} не выведен из «предупреждение — ошибка»");

            Assert.IsTrue(Regex.IsMatch(props, @"<WarningsNotAsErrors>[^<]*NU1903"),
                "Коды перечислены не в том свойстве");
        }

        /// <summary>
        /// Обновления зависимостей приходят сами — и пакеты, и действия
        /// рабочих процессов. У действий версия закреплена только этим
        /// файлом: ссылка вида «@v4» указывает на подвижную метку.
        /// </summary>
        [TestMethod]
        public void Dependabot_WatchesPackagesAndActions()
        {
            var config = Read(".github", "dependabot.yml");

            StringAssert.Contains(config, "version: 2");
            StringAssert.Contains(config, "package-ecosystem: \"nuget\"");
            StringAssert.Contains(config, "package-ecosystem: \"github-actions\"");
            StringAssert.Contains(config, "interval: \"weekly\"");
        }

        // ------------------------------------------------------------------
        // Вспомогательное
        // ------------------------------------------------------------------

        /// <summary>Пишет заготовку ответа во временный файл.</summary>
        private static string Listing(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }

        /// <summary>
        /// Запускает проверку на готовом ответе и возвращает её код и вывод.
        ///
        /// Скрипт запускается, а не читается: он компилируется не компилятором
        /// C#, и его правка не ломает ни сборку, ни тесты — расхождение
        /// увидели бы только на упавшем или, что хуже, на зелёном прогоне.
        /// </summary>
        /// <param name="listingPath">Готовый ответ «dotnet list package».</param>
        private static (int ExitCode, string Output) RunScript(string listingPath)
        {
            var script = Path.Combine(Root, "build", "Test-VulnerablePackages.ps1");
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
                         "-File", script, "-JsonPath", listingPath
                     })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            Assert.IsTrue(process.WaitForExit(60000), "Проверка не завершилась за минуту");

            return (process.ExitCode, output);
        }
    }
}
