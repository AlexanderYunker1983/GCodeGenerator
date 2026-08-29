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
    /// свойства, потеря которых обходится дорого: запущенная программа
    /// закрывается по-хорошему, а вывод можно подписать.
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
        /// Запущенную программу сначала просят закрыться. Принудительное
        /// завершение не даёт ей ничего выполнить, то есть уносит несохранённый
        /// проект без вопроса; мягкое закрытие идёт через окно, и программа
        /// спрашивает о сохранении сама.
        /// </summary>
        [TestMethod]
        public void Installer_AsksTheAppToCloseBeforeKillingIt()
        {
            var script = InstallerScript;

            var graceful = script.IndexOf("'/IM ' + AppExeName", StringComparison.Ordinal);
            var forced = script.IndexOf("'/F /IM ' + AppExeName", StringComparison.Ordinal);

            Assert.IsTrue(graceful >= 0,
                "install/GCodeGenerator.iss: нет мягкого закрытия (taskkill без /F) — "
                + "принудительное завершение уносит несохранённый проект без вопроса");
            Assert.IsTrue(forced < 0 || graceful < forced,
                "install/GCodeGenerator.iss: принудительное завершение стоит раньше просьбы закрыться");
        }

        /// <summary>
        /// Принудительное завершение предлагается отдельным вопросом, где
        /// названа его цена. Молчаливый переход к нему после неудачной
        /// попытки вернул бы прежнее поведение другим путём.
        /// </summary>
        [TestMethod]
        public void Installer_WarnsAboutDataLossBeforeForcedKill()
        {
            var script = InstallerScript;

            StringAssert.Contains(script, "AppStillRunningForceQuestion",
                "install/GCodeGenerator.iss: перед принудительным завершением не задаётся вопрос");

            foreach (var language in new[] { "english", "russian" })
            {
                var message = Regex.Match(
                    script,
                    @"^" + language + @"\.AppStillRunningForceQuestion=(.+)$",
                    RegexOptions.Multiline);

                Assert.IsTrue(message.Success, $"Нет сообщения на языке {language}");
                Assert.IsTrue(
                    message.Groups[1].Value.Contains("Unsaved changes will be lost")
                    || message.Groups[1].Value.Contains("Несохранённые изменения будут потеряны"),
                    $"{language}: в вопросе не сказано о потере несохранённых изменений");
            }
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
