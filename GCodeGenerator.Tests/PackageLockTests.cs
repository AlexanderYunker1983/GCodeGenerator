using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Замок пакетов покрывает ту сборку, которая уходит пользователям.
    ///
    /// Состав зависимостей закреплён файлами <c>packages.lock.json</c>, а
    /// рабочие процессы восстанавливают пакеты в закреплённом режиме, где
    /// расхождение с замком — отказ, а не молчаливое обновление. Для сборки
    /// инсталлятора это долго не работало: она публикует продукт под win-x64,
    /// а восстановление под средой выполнения добавляет в дерево зависимостей
    /// собственный раздел, которого в замке не было. Закреплённый режим
    /// отвергал такое восстановление (NU1004), поэтому публикация шла в обход
    /// замка и переписывала его на ходу.
    ///
    /// Держится это на трёх файлах сразу: среда объявлена в csproj, разделы
    /// под неё лежат в замках, а скрипт публикует в закреплённом режиме.
    /// Разойтись они могут молча — обычная сборка и тесты не заметят ничего,
    /// а узнать об этом можно было бы только собрав инсталлятор.
    /// </summary>
    [TestClass]
    public class PackageLockTests
    {
        /// <summary>Среда выполнения, под которую публикуется продукт.</summary>
        private const string PublishRuntime = "win-x64";

        /// <summary>
        /// Проекты, попадающие в публикацию: приложение и ядро, на которое
        /// оно ссылается. Восстановление под средой идёт по всему дереву
        /// ссылок, поэтому раздел нужен в обоих замках.
        /// </summary>
        private static readonly string[] PublishedProjects =
        {
            "GCodeGenerator",
            "GCodeGenerator.Core",
        };

        private static string Root => RepositoryRootLocator.Find();

        /// <summary>
        /// Замок пакетов воспроизводим только вместе с тем SDK, чей NuGet
        /// построил граф зависимостей. Минимальная версия с разрешённым
        /// roll-forward выглядит закреплённой, но на новом runner молча
        /// выбирает другой набор инструментов.
        /// </summary>
        [TestMethod]
        public void GlobalJson_PinsTheExactStableSdk()
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(Root, "global.json")));
            var sdk = document.RootElement.GetProperty("sdk");

            Assert.AreEqual("10.0.302", sdk.GetProperty("version").GetString(),
                "Версия SDK — часть воспроизводимой сборки");
            Assert.AreEqual("disable", sdk.GetProperty("rollForward").GetString(),
                "SDK не должен молча переходить на другой patch или feature band");
            Assert.IsFalse(sdk.GetProperty("allowPrerelease").GetBoolean(),
                "Публичный релиз не собирается предварительной версией SDK");
        }

        [TestMethod]
        public void PublishedProjects_DeclareThePublishRuntime()
        {
            foreach (var project in PublishedProjects)
            {
                var csproj = File.ReadAllText(
                    Path.Combine(Root, project, project + ".csproj"));

                var declared = Regex.Match(csproj, @"<RuntimeIdentifiers>([^<]*)</RuntimeIdentifiers>");

                Assert.IsTrue(declared.Success,
                    $"{project}.csproj: не объявлена среда выполнения (RuntimeIdentifiers). "
                    + "Без неё замок не покрывает публикацию, и она идёт мимо закреплённого состава пакетов.");
                CollectionAssert.Contains(
                    declared.Groups[1].Value.Split(';').Select(rid => rid.Trim()).ToArray(),
                    PublishRuntime,
                    $"{project}.csproj: среди объявленных сред нет {PublishRuntime}");
            }
        }

        /// <summary>
        /// В замке есть оба раздела: без среды выполнения — для обычной сборки
        /// и тестов, со средой — для публикации. Закрепить можно только оба
        /// сразу: замок с одним из них отвергает восстановление для другого.
        /// </summary>
        [TestMethod]
        public void PublishedProjects_LockBothPlainAndRuntimeTargets()
        {
            foreach (var project in PublishedProjects)
            {
                var targets = LockTargets(project);

                var plain = targets.Where(target => !target.Contains('/')).ToList();
                Assert.AreEqual(1, plain.Count,
                    $"{project}: ожидался один раздел без среды выполнения, есть [{string.Join(", ", targets)}]");

                var expected = plain[0] + "/" + PublishRuntime;
                CollectionAssert.Contains(targets, expected,
                    $"{project}: в замке нет раздела {expected}. Пересоздайте замки восстановлением "
                    + "с ключом force-evaluate — публикация под этой средой иначе не проверяется.");
            }
        }

        /// <summary>
        /// Проекты, не попадающие в публикацию, среду не объявляют: лишний
        /// раздел в их замках пришлось бы поддерживать без всякой пользы.
        /// </summary>
        [TestMethod]
        public void TestProjects_LockOnlyThePlainTarget()
        {
            foreach (var project in new[] { "GCodeGenerator.Tests", "GCodeGenerator.Core.Tests" })
            {
                var targets = LockTargets(project);

                CollectionAssert.AreEqual(
                    targets.Where(target => !target.Contains('/')).ToArray(),
                    targets.ToArray(),
                    $"{project}: тестовый проект не публикуется, разделы среды выполнения ему не нужны");
            }
        }

        /// <summary>
        /// Инсталлятор собирается в закреплённом режиме. Ключа командной
        /// строки у публикации нет, поэтому режим включается свойством
        /// MSBuild — и потерять его правкой скрипта легко.
        /// </summary>
        [TestMethod]
        public void Installer_PublishesWithLockedRestore()
        {
            var script = File.ReadAllText(Path.Combine(Root, "build", "Make-Installer.ps1"));

            StringAssert.Contains(script, "-p:RestoreLockedMode=true",
                "build/Make-Installer.ps1: публикация обязана идти в закреплённом режиме, "
                + "иначе состав пакетов инсталлятора ничем не закреплён");
        }

        /// <summary>Разделы зависимостей замка проекта.</summary>
        /// <param name="project">Каталог проекта в корне репозитория.</param>
        private static List<string> LockTargets(string project)
        {
            var path = Path.Combine(Root, project, "packages.lock.json");
            Assert.IsTrue(File.Exists(path), $"Нет замка пакетов: {path}");

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement
                .GetProperty("dependencies")
                .EnumerateObject()
                .Select(target => target.Name)
                .ToList();
        }
    }
}
