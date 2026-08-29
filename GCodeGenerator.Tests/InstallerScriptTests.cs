using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Правила установщика, которые нельзя проверить сборкой.
    ///
    /// Скрипт Inno Setup и скрипт сборки компилируются не компилятором C#,
    /// поэтому их правки не ломают ни сборку, ни прогон тестов: расхождение
    /// увидели бы только на машине пользователя. Здесь закреплены два
    /// свойства, потеря которых обходится дорого: обновляется именно
    /// установленный экземпляр, а вывод можно подписать.
    /// </summary>
    [TestClass]
    public class InstallerScriptTests
    {
        private static string Root => RepositoryRootLocator.Find();

        private static string InstallerScript
            => File.ReadAllText(Path.Combine(Root, "install", "GCodeGenerator.iss"));

        private static string BuildScript
            => File.ReadAllText(Path.Combine(Root, "build", "Make-Installer.ps1"));

        /// <summary>
        /// Windows Restart Manager определяет процесс по файлам из {app},
        /// а не по общему имени exe: portable-копия из другого каталога не
        /// должна быть закрыта обновлением установленной версии.
        /// </summary>
        [TestMethod]
        public void Installer_UsesPathAwareRestartManagerWithoutForcedKill()
        {
            var script = InstallerScript;

            Assert.IsTrue(Regex.IsMatch(script, @"^CloseApplications=yes\s*$", RegexOptions.Multiline));
            Assert.IsTrue(Regex.IsMatch(script, @"^RestartApplications=yes\s*$", RegexOptions.Multiline));
            Assert.IsFalse(script.Contains("tasklist", StringComparison.OrdinalIgnoreCase),
                "Поиск по имени видит чужие portable-копии");
            Assert.IsFalse(script.Contains("taskkill", StringComparison.OrdinalIgnoreCase),
                "Глобальный taskkill может уничтожить несохранённую работу другого экземпляра");
            Assert.IsFalse(script.Contains("CloseApplications=force", StringComparison.OrdinalIgnoreCase),
                "Принудительное закрытие обходит вопрос приложения о сохранении");
        }

        /// <summary>
        /// Свои сообщения установщика переведены на оба языка мастера.
        /// Русский выбирается по языку системы автоматически, и вопрос,
        /// заданный при этом по-английски, — единственное, что пользователь
        /// от установщика вообще слышит.
        /// </summary>
        [TestMethod]
        public void Installer_TranslatesItsOwnMessages()
        {
            var script = InstallerScript;
            var messages = Regex.Matches(script, @"^(english|russian)\.([A-Za-z]+)=", RegexOptions.Multiline)
                .Cast<Match>()
                .ToLookup(match => match.Groups[2].Value, match => match.Groups[1].Value);

            Assert.IsTrue(messages.Any(), "install/GCodeGenerator.iss: секция [CustomMessages] пуста");

            foreach (var message in messages)
            {
                CollectionAssert.AreEquivalent(
                    new[] { "english", "russian" },
                    message.Distinct().ToArray(),
                    $"Сообщение {message.Key} переведено не на все языки мастера");
            }
        }

        /// <summary>
        /// Русский текст в скрипте установщика требует BOM: без него Inno Setup
        /// читает файл как ANSI, и сообщения превращаются в мусор — молча,
        /// уже в собранном установщике.
        /// </summary>
        [TestMethod]
        public void InstallerScript_IsUtf8WithBom()
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "install", "GCodeGenerator.iss"));

            CollectionAssert.AreEqual(
                Encoding.UTF8.GetPreamble(),
                bytes.Take(3).ToArray(),
                "install/GCodeGenerator.iss: нет метки UTF-8 (BOM), а в файле есть русский текст");
        }

        /// <summary>
        /// Подпись приходит извне, но стабильный выпуск без неё запрещён.
        /// Pre-release можно собирать неподписанным для тестирования.
        /// </summary>
        [TestMethod]
        public void Build_SupportsOptionalSigning()
        {
            var build = BuildScript;

            StringAssert.Contains(build, "$SignCommand",
                "build/Make-Installer.ps1: нет параметра команды подписи");
            StringAssert.Contains(build, "GCODEGEN_SIGN_COMMAND",
                "build/Make-Installer.ps1: команду подписи нельзя задать переменной окружения");
            StringAssert.Contains(build, "/DSignToolName=",
                "build/Make-Installer.ps1: подпись не передаётся компилятору установщика");
            StringAssert.Contains(build, "$suffix -eq ''",
                "Стабильная версия не отличается от pre-release при проверке подписи");
            StringAssert.Contains(build, "A stable release must be code-signed",
                "Стабильный выпуск не останавливается без сертификата");
            StringAssert.Contains(build, "$AllowUnsignedStable",
                "Нет явного локального обхода для диагностической сборки");

            StringAssert.Contains(InstallerScript, "#ifdef SignToolName",
                "install/GCodeGenerator.iss: SignTool объявлен безусловно — "
                + "сборка без сертификата перестанет компилироваться");
            StringAssert.Contains(InstallerScript, "SignedUninstaller=yes",
                "install/GCodeGenerator.iss: деинсталлятор остаётся неподписанным");
        }

        /// <summary>
        /// Символы нужны для локальной диагностики, но не в установщике:
        /// publish-каталог целиком копируется также в portable ZIP и иначе
        /// раскрывает внутренние пути/имена, одновременно раздувая выпуск.
        /// </summary>
        [TestMethod]
        public void Build_DoesNotPackageDebugSymbols()
        {
            var build = BuildScript;

            StringAssert.Contains(build, "-p:DebugSymbols=false");
            StringAssert.Contains(build, "-p:DebugType=None");
            StringAssert.Contains(build, "-Filter '*.pdb' -File -Recurse",
                "После publish нет fail-closed проверки фактического содержимого");
            StringAssert.Contains(build, "Publish output contains debug symbols");
        }

        [TestMethod]
        public void Release_VerifiesDownloadedInnoSetupBeforeExecution()
        {
            var workflow = File.ReadAllText(Path.Combine(
                Root, ".github", "workflows", "release.yml"));

            StringAssert.Contains(workflow,
                "9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732",
                "SHA-256 официального Inno Setup 6.7.3 не закреплён");
            StringAssert.Contains(workflow, "Get-FileHash -Algorithm SHA256",
                "Загруженный exe запускается без вычисления хеша");
            Assert.IsTrue(workflow.IndexOf("Get-FileHash -Algorithm SHA256", StringComparison.Ordinal)
                          < workflow.IndexOf("& $installer /VERYSILENT", StringComparison.Ordinal),
                "Проверка хеша должна предшествовать запуску загруженного exe");
        }

        /// <summary>
        /// Установщик связывает с программой файлы проектов — иначе двойной
        /// щелчок по <c>.ygc</c> ничего не открывает, а формат этот собственный
        /// и открыть его больше нечем.
        ///
        /// Связь — задача мастера, а не данность: расширение может быть уже
        /// занято, и решать это пользователю. Путь к программе передаётся
        /// в кавычках: проект в каталоге с пробелом иначе придёт двумя
        /// аргументами и не откроется.
        /// </summary>
        [TestMethod]
        public void Installer_AssociatesProjectFiles()
        {
            var script = InstallerScript;

            StringAssert.Contains(script, @"Subkey: ""Software\Classes\.ygc""",
                "install/GCodeGenerator.iss: расширение .ygc не связывается с программой");
            // Кавычки в значении .iss удваиваются, поэтому в файле лежит
            // «""{app}\...exe"" ""%1""» — на выходе это одна пара на каждую часть.
            StringAssert.Contains(script, @"""""{app}\GCodeGenerator.exe"""" """"%1""""",
                "Путь к открываемому файлу должен передаваться в кавычках");
            StringAssert.Contains(script, "Tasks: associate",
                "Связь с файлами должна быть задачей, от которой можно отказаться");
            StringAssert.Contains(script, "ChangesAssociations=yes",
                "Оболочку нужно уведомить, иначе иконка и пункт меню появятся не сразу");
            StringAssert.Contains(script, "uninsdeletekey",
                "Удаление программы должно убирать за собой описание типа файла");
        }

        /// <summary>
        /// Программу можно поставить без прав администратора.
        ///
        /// Прежде мастер требовал их безусловно, и на рабочем компьютере
        /// с ограниченной учётной записью установка была невозможна вовсе —
        /// при том что программа никакой части системы не касается.
        /// Ключами те же два режима доступны и тихой установке, которая
        /// на вопрос мастера ответить не может.
        /// </summary>
        [TestMethod]
        public void Installer_CanBeInstalledWithoutAdminRights()
        {
            var script = InstallerScript;

            Assert.IsTrue(Regex.IsMatch(script, @"^PrivilegesRequired=lowest\s*$", RegexOptions.Multiline),
                "install/GCodeGenerator.iss: установка по-прежнему требует прав администратора");

            var allowed = Regex.Match(script, @"^PrivilegesRequiredOverridesAllowed=(?<modes>.+)$",
                RegexOptions.Multiline);

            Assert.IsTrue(allowed.Success, "Выбор режима установки не разрешён");
            StringAssert.Contains(allowed.Groups["modes"].Value, "dialog",
                "Мастер не спрашивает, для кого ставить");
            StringAssert.Contains(allowed.Groups["modes"].Value, "commandline",
                "Тихой установке нечем выбрать режим");
        }

        /// <summary>
        /// Документация и установщик не должны обещать Windows 10, которую
        /// уже не поддерживает ни сама ОС, ни поставляемый .NET 10 runtime.
        /// Portable остаётся технически запускаемым файлом, но граница
        /// официальной поддержки должна быть одна и недвусмысленная.
        /// </summary>
        [TestMethod]
        public void Installer_AndReadmesRequireWindows11Build26100()
        {
            var russian = File.ReadAllText(Path.Combine(Root, "README.md"));
            var english = File.ReadAllText(Path.Combine(Root, "README.en.md"));

            StringAssert.Contains(InstallerScript, "MinVersion=10.0.26100");
            StringAssert.Contains(russian, "Windows 11 24H2 (build 26100)");
            StringAssert.Contains(english, "Windows 11 24H2 (build 26100)");
            Assert.IsFalse(russian.Contains("минимальная поддерживаемая Windows 10"));
            Assert.IsFalse(english.Contains("minimum supported Windows 10"));
        }

        /// <summary>
        /// Каталог, ярлыки и ветка реестра выбираются режимом установки сами.
        ///
        /// Это и делает выбор режима возможным: жёстко заданный
        /// <c>{pf}</c> или <c>HKLM</c> потребовал бы прав администратора
        /// в обход самой настройки — установка «только для себя» падала бы
        /// на записи, а не отказывалась начинаться.
        /// </summary>
        [TestMethod]
        public void Installer_PathsFollowTheInstallMode()
        {
            var script = InstallerScript;

            foreach (var constant in new[] { "{autopf}", "{autodesktop}", "Root: HKA" })
                StringAssert.Contains(script, constant, $"Не используется {constant}");

            // Директивы и записи, а не комментарии: в них эти имена
            // упоминаются как раз при объяснении, почему их здесь нет.
            var directives = string.Join("\n", script
                .Split('\n')
                .Where(line => !line.TrimStart().StartsWith(";", StringComparison.Ordinal)));

            foreach (var fixedPath in new[]
                     {
                         "{pf}", "{pf32}", "{pf64}", "{commonpf}", "{userpf}",
                         "{commondesktop}", "{userdesktop}",
                         "Root: HKLM", "Root: HKCU"
                     })
            {
                Assert.IsFalse(directives.Contains(fixedPath, StringComparison.Ordinal),
                    $"{fixedPath} задан жёстко и не следует режиму установки");
            }
        }

        /// <summary>
        /// Скрипты сборки — только ASCII: Windows PowerShell 5.1 читает
        /// .ps1 без BOM как ANSI, и не-ASCII текст в них искажается.
        /// Правило объявлено в шапке самого скрипта.
        /// </summary>
        [TestMethod]
        public void BuildScripts_AreAsciiOnly()
        {
            foreach (var path in Directory.EnumerateFiles(Path.Combine(Root, "build"), "*.ps1"))
            {
                var offending = File.ReadAllBytes(path)
                    .Select((value, index) => (Value: value, Index: index))
                    .Where(byteAt => byteAt.Value > 127)
                    .Take(3)
                    .ToList();

                Assert.AreEqual(0, offending.Count,
                    $"{Path.GetFileName(path)}: не-ASCII байты в позициях "
                    + string.Join(", ", offending.Select(byteAt => byteAt.Index)));
            }
        }
    }
}
