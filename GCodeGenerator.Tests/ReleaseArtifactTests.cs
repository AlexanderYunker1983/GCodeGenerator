using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Проверки файлов, которыми пользователь сверяет выпуск.</summary>
    [TestClass]
    public class ReleaseArtifactTests
    {
        private static string Root => RepositoryRootLocator.Find();

        [TestMethod]
        public void ChecksumScript_ProducesSortedSha256ForEveryDistributedBinary()
        {
            var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            var installer = Path.Combine(directory, "GCodeGenerator-Setup-1.2.3.exe");
            var portable = Path.Combine(directory, "GCodeGenerator-1.2.3-win-x64-portable.zip");
            var sbom = Path.Combine(directory, "GCodeGenerator-1.2.3-sbom.cdx.json");
            var ignored = Path.Combine(directory, "release-notes.md");
            File.WriteAllBytes(installer, new byte[] { 0, 1, 2, 3 });
            File.WriteAllBytes(portable, new byte[] { 9, 8, 7 });
            File.WriteAllBytes(sbom, new byte[] { 4, 5, 6 });
            File.WriteAllText(ignored, "not executable");

            try
            {
                var script = Path.Combine(Root, "build", "New-ReleaseChecksums.ps1");
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
                             "-File", script, "-Directory", directory
                         })
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEnd();
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);
                Assert.AreEqual(0, process.ExitCode, output + errors);

                var checksumPath = Path.Combine(directory, "SHA256SUMS.txt");
                var bytes = File.ReadAllBytes(checksumPath);
                Assert.IsFalse(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
                    "BOM не является частью формата checksum");

                var lines = File.ReadAllLines(checksumPath);
                var expected = new[] { portable, installer, sbom }
                    .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .Select(path =>
                        $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}  {Path.GetFileName(path)}")
                    .ToArray();
                CollectionAssert.AreEqual(expected, lines);
                Assert.IsFalse(File.ReadAllText(checksumPath).Contains("release-notes"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void ReleaseWorkflow_PublishesTheChecksumFile()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));

            StringAssert.Contains(workflow, "build/New-ReleaseChecksums.ps1");
            StringAssert.Contains(workflow, "release-assets/SHA256SUMS.txt");
        }

        [TestMethod]
        public void ReleaseWorkflow_RequiresCurrentMasterAndAllQualityGatesBeforePackaging()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));

            foreach (var required in new[]
                     {
                         "git rev-list -n 1 --verify \"refs/tags/$env:TAG_NAME\"",
                         "git rev-parse refs/remotes/origin/master",
                         "dotnet build GCodeGenerator.sln -c Release --no-restore -warnaserror",
                         "Code Coverage;Format=cobertura",
                         "build/Assert-Coverage.ps1",
                         "-Assembly GCodeGenerator.Core",
                         "-Assembly GCodeGenerator",
                         "build/Test-VulnerablePackages.ps1",
                         "dotnet stryker --config-file stryker-config.json --skip-version-check"
                     })
            {
                StringAssert.Contains(workflow, required);
            }

            var masterCheck = workflow.IndexOf("Verify tag points to current master", StringComparison.Ordinal);
            var build = workflow.IndexOf("Build (Release)", StringComparison.Ordinal);
            var coverage = workflow.IndexOf("Enforce coverage thresholds", StringComparison.Ordinal);
            var vulnerabilities = workflow.IndexOf("Check for vulnerable packages", StringComparison.Ordinal);
            var mutation = workflow.IndexOf("Run mutation tests", StringComparison.Ordinal);
            var installer = workflow.IndexOf("Build installer", StringComparison.Ordinal);
            Assert.IsTrue(
                masterCheck < build && build < coverage && coverage < vulnerabilities &&
                vulnerabilities < mutation && mutation < installer,
                "Упаковка начинается до завершения release quality gates");
        }

        [TestMethod]
        public void SbomScript_ListsApplicationAndTransitivePackages()
        {
            var outputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cdx.json");
            try
            {
                var script = Path.Combine(Root, "build", "New-ReleaseSbom.ps1");
                var project = Path.Combine(Root, "GCodeGenerator", "GCodeGenerator.csproj");
                var start = new ProcessStartInfo("powershell.exe")
                {
                    WorkingDirectory = Root,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                foreach (var argument in new[]
                         {
                             "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                             "-File", script, "-Project", project, "-Version", "1.2.3", "-OutputPath", outputPath
                         })
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEnd();
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);
                Assert.AreEqual(0, process.ExitCode, output + errors);

                using var document = JsonDocument.Parse(File.ReadAllBytes(outputPath));
                var root = document.RootElement;
                Assert.AreEqual("CycloneDX", root.GetProperty("bomFormat").GetString());
                Assert.AreEqual("1.6", root.GetProperty("specVersion").GetString());
                Assert.AreEqual("1.2.3",
                    root.GetProperty("metadata").GetProperty("component").GetProperty("version").GetString());

                var names = root.GetProperty("components")
                    .EnumerateArray()
                    .Select(component => component.GetProperty("name").GetString())
                    .ToArray();
                CollectionAssert.Contains(names, "Autofac", "Прямая зависимость не попала в SBOM");
                CollectionAssert.Contains(names, "Clipper2", "Транзитивная зависимость не попала в SBOM");
                CollectionAssert.Contains(names, "netDxf", "Зависимость ядра не попала в SBOM");
                CollectionAssert.DoesNotContain(names, "MSTest.TestFramework",
                    "Тестовый пакет не поставляется приложением и не должен быть в SBOM");
            }
            finally
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
            }
        }

        [TestMethod]
        public void ReleaseWorkflow_PublishesSbomBeforeCalculatingChecksums()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
            var sbom = workflow.IndexOf("build/New-ReleaseSbom.ps1", StringComparison.Ordinal);
            var checksums = workflow.IndexOf("build/New-ReleaseChecksums.ps1", StringComparison.Ordinal);

            Assert.IsTrue(sbom >= 0 && sbom < checksums, "SBOM должен существовать до вычисления SHA-256");
            StringAssert.Contains(workflow, "release-assets/GCodeGenerator-*-sbom.cdx.json");
        }

        [TestMethod]
        public void ReleaseWorkflow_AttestsChecksummedFilesBeforePublishing()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
            var attest = System.Text.RegularExpressions.Regex.Match(
                workflow,
                @"(?ms)^  attest:\s(?<body>.*?)(?=^  publish:)");

            Assert.IsTrue(attest.Success, "Нет отдельного job аттестации");
            StringAssert.Contains(attest.Groups["body"].Value, "id-token: write");
            StringAssert.Contains(attest.Groups["body"].Value, "attestations: write");
            StringAssert.Contains(attest.Groups["body"].Value, "subject-checksums: release-assets/SHA256SUMS.txt");
            StringAssert.Contains(workflow, "needs: [build, attest]",
                "Публикация не ждёт успешной аттестации");
        }

        [TestMethod]
        public void ReleaseWorkflow_PublishesACompleteImmutableDraftWithoutOverwritingAssets()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
            var createDraft = workflow.IndexOf("- name: Create complete draft release", StringComparison.Ordinal);
            var publishDraft = workflow.IndexOf("gh release edit $env:GITHUB_REF_NAME", StringComparison.Ordinal);
            var verifyRelease = workflow.IndexOf("gh release verify $env:GITHUB_REF_NAME", StringComparison.Ordinal);

            StringAssert.Contains(workflow, "draft: true",
                "Assets загружаются прямо в публичный release, где immutability уже запрещает дополнения");
            StringAssert.Contains(workflow, "overwrite_files: false",
                "Повторный workflow может заменить ранее загруженный asset");
            Assert.IsFalse(workflow.Contains("overwrite: true", StringComparison.Ordinal),
                "Оставлен неподдерживаемый и небезопасный флаг перезаписи release");
            Assert.IsTrue(createDraft >= 0 && createDraft < publishDraft && publishDraft < verifyRelease,
                "Нарушен порядок draft -> publish -> immutable verification");
            StringAssert.Contains(workflow,
                "gh release verify-asset $env:GITHUB_REF_NAME $asset.FullName",
                "После публикации не проверяются attestation всех локальных assets");
            StringAssert.Contains(workflow,
                "gh release edit $env:GITHUB_REF_NAME --repo $env:GITHUB_REPOSITORY --draft",
                "Mutable-выпуск не возвращается в draft при неудачной проверке");
            StringAssert.Contains(workflow, "for ($attempt = 1; $attempt -le 30; $attempt++)",
                "Минутного ожидания недостаточно для асинхронной release attestation");
            StringAssert.Contains(workflow, "--json isImmutable,isDraft,url");
            var immutableBranch = workflow.IndexOf("if ($releaseState.isImmutable)", StringComparison.Ordinal);
            var mutableRollback = workflow.IndexOf(
                "gh release edit $env:GITHUB_REF_NAME --repo $env:GITHUB_REPOSITORY --draft",
                immutableBranch,
                StringComparison.Ordinal);
            Assert.IsTrue(immutableBranch >= 0 && mutableRollback > immutableBranch,
                "Состояние immutable должно проверяться до попытки вернуть выпуск в draft");
            StringAssert.Contains(workflow, "Do not rerun publish. Verify manually",
                "При задержке immutable attestation workflow должен направлять к ручной проверке");
        }

        [TestMethod]
        public void ReleaseWorkflow_ExercisesInstallerUpgradePortableAndUninstall()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
            var script = File.ReadAllText(Path.Combine(Root, "build", "Test-PackagedArtifacts.ps1"));

            StringAssert.Contains(workflow, "build/Test-PackagedArtifacts.ps1");
            StringAssert.Contains(workflow, "packaged-artifact-smoke.log");
            StringAssert.Contains(workflow, "if: always() && steps.tag.outputs.skip == 'false'",
                "Диагностический лог потеряется при падении smoke-теста");
            foreach (var stage in new[]
                     {
                         "'Install previous release'",
                         "'Start previous release'",
                         "'Upgrade previous release to candidate'",
                         "'Start upgraded candidate'",
                         "'Start portable application'",
                         "'Uninstall'"
                     })
            {
                StringAssert.Contains(script, stage, $"Не автоматизирован этап {stage}");
            }

            StringAssert.Contains(script, "/CURRENTUSER");
            StringAssert.Contains(script, "CloseMainWindow()",
                "Приложение принудительно убивается вместо проверки штатного закрытия");
            StringAssert.Contains(script, "Installed executable remains after uninstall");
            StringAssert.Contains(workflow, "gh release list");
            StringAssert.Contains(workflow, "gh release download");
            StringAssert.Contains(workflow, "PreviousInstallerPath = '${{ steps.previous.outputs.path }}'");
            StringAssert.Contains(workflow, "PortableArchivePath = $portableArchive");
            StringAssert.Contains(script, "Expand-Archive -LiteralPath $portableArchive");
            StringAssert.Contains(script, "$portableExe = Join-Path $portableDirectory 'GCodeGenerator.exe'",
                "Smoke должен запускать exe из распакованного архива");
            Assert.IsFalse(script.Contains("PortableExePath", StringComparison.Ordinal),
                "Portable по-прежнему проверяется напрямую из publish-каталога");
            StringAssert.Contains(workflow,
                "55c92678f32d500d103f40a777cc93c182d030b63fbf776acffe8a067cdaac6f",
                "SHA-256 предыдущего legacy-инсталлятора не закреплён");
            StringAssert.Contains(workflow, "Get-FileHash -LiteralPath $previousInstaller -Algorithm SHA256",
                "Скачанный legacy-инсталлятор запускается без проверки SHA-256");
            StringAssert.Contains(workflow, "$installerAssets[0].name -cne $legacyTrust.Name",
                "Имя legacy-инсталлятора не закреплено вместе с хэшем");
            StringAssert.Contains(workflow, "--json assets,isImmutable",
                "Следующие источники обновления не обязаны быть immutable");
            StringAssert.Contains(workflow, "gh release verify $candidate.tagName");
            StringAssert.Contains(workflow,
                "gh release verify-asset $candidate.tagName $previousInstaller",
                "Артефакт следующего immutable-выпуска не проверяется по release attestation");
            StringAssert.Contains(workflow, "build/Make-Installer.ps1 -SigningMode Unsigned",
                "Активная политика unsigned-выпуска не задана явно");
            Assert.IsFalse(workflow.Contains("secrets.SIGN_COMMAND", StringComparison.Ordinal),
                "Секрет команды подписи доступен шагам, запускающим сторонний установщик");
            Assert.IsFalse(workflow.Contains("secrets.SIGNER_THUMBPRINT", StringComparison.Ordinal),
                "Секрет сертификата доступен шагам, запускающим сторонний установщик");
            Assert.IsFalse(workflow.Contains("GCODEGEN_SIGN_COMMAND:", StringComparison.Ordinal),
                "Команда подписи экспортируется в окружение release job");
            StringAssert.Contains(script, "RequireAuthenticodeSignature");
            StringAssert.Contains(script, "Assert-AuthenticodeSignature.ps1");
            StringAssert.Contains(script, "'Upgraded payload'");

            var install = script.IndexOf("'Install previous release'", StringComparison.Ordinal);
            var upgrade = script.IndexOf("'Upgrade previous release to candidate'", StringComparison.Ordinal);
            var uninstall = script.IndexOf("'Uninstall'", StringComparison.Ordinal);
            var legacyDigest = workflow.IndexOf("Get-FileHash -LiteralPath $previousInstaller", StringComparison.Ordinal);
            var exposePreviousPath = workflow.IndexOf(
                "Add-Content -Path $env:GITHUB_OUTPUT",
                legacyDigest,
                StringComparison.Ordinal);
            Assert.IsTrue(legacyDigest < exposePreviousPath,
                "Путь к legacy-инсталлятору публикуется для запуска до проверки хэша");
            Assert.IsTrue(install < upgrade && upgrade < uninstall, "Этапы жизненного цикла перепутаны");
        }

        [TestMethod]
        public void ReleaseWorkflow_ManualRehearsalCannotPublish()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));

            StringAssert.Contains(workflow, "workflow_dispatch:",
                "Packaged smoke нельзя заранее выполнить на hosted runner");
            StringAssert.Contains(workflow,
                "TAG_NAME: ${{ github.event_name == 'workflow_dispatch' && inputs.version || github.ref_name }}");
            Assert.AreEqual(2,
                Regex.Matches(
                    workflow,
                    @"(?m)^    if: needs\.build\.outputs\.skip == 'false' && github\.event_name == 'push'\r?$")
                    .Count,
                "Attest и publish должны быть недоступны ручной репетиции");
            StringAssert.Contains(workflow,
                "if: steps.tag.outputs.skip == 'false' && github.event_name == 'push'",
                "Ручной прогон не должен требовать существующий git-тег");
        }

        [TestMethod]
        public void ReleaseWorkflow_AndPackagedProcesses_HaveExplicitTimeouts()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "release.yml"));
            var script = File.ReadAllText(Path.Combine(Root, "build", "Test-PackagedArtifacts.ps1"));

            const string timeoutPattern = @"(?m)^    timeout-minutes: \d+\r?$";
            var lfWorkflow = workflow.Replace("\r\n", "\n", StringComparison.Ordinal);
            var crlfWorkflow = lfWorkflow.Replace("\n", "\r\n", StringComparison.Ordinal);
            foreach (var (lineEndings, text) in new[]
                     {
                         ("LF", lfWorkflow),
                         ("CRLF", crlfWorkflow)
                     })
            {
                Assert.AreEqual(3,
                    System.Text.RegularExpressions.Regex.Matches(text, timeoutPattern).Count,
                    $"Каждый release job должен иметь верхнюю границу времени ({lineEndings})");
            }
            StringAssert.Contains(script, "[int]$ProcessTimeoutSeconds = 300");
            StringAssert.Contains(script, "$process.WaitForExit($processTimeoutMilliseconds)",
                "Установщик или деинсталлятор может зависнуть навсегда");
            StringAssert.Contains(script, "$process.WaitForExit($closeTimeoutMilliseconds)",
                "Закрытие приложения должно быть ограничено по времени");
            StringAssert.Contains(script, "[int]$CleanupTimeoutSeconds = 10");
            StringAssert.Contains(script, "Remove-SmokeWorkDirectory");
            StringAssert.Contains(script, "Start-Sleep -Milliseconds 200",
                "Краткая блокировка лога деинсталлятором не должна ложно ронять smoke-тест");
            Assert.IsFalse(script.Contains("-Wait -PassThru", StringComparison.Ordinal),
                "Start-Process -Wait не позволяет контролировать тайм-аут");
            StringAssert.Contains(script, "timed out after $ProcessTimeoutSeconds seconds");
            StringAssert.Contains(workflow, "timeout-minutes: 150",
                "Полный release build не укладывается в прежний 90-минутный бюджет");
        }

        [TestMethod]
        public void ReleaseNotices_AreCopiedIntoTheSharedPublishDirectory()
        {
            var publish = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(publish);
            try
            {
                var script = Path.Combine(Root, "build", "Copy-ReleaseNotices.ps1");
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
                             "-File", script, "-PublishDirectory", publish, "-RepositoryRoot", Root
                         })
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEnd();
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);
                Assert.AreEqual(0, process.ExitCode, output + errors);

                var licenses = Path.Combine(publish, "licenses");
                foreach (var name in new[]
                         {
                             "GCodeGenerator-LICENSE.txt",
                             "THIRD-PARTY-NOTICES.md",
                             "DOTNET-LICENSE.txt",
                             "DOTNET-THIRD-PARTY-NOTICES.txt",
                             "COMMUNITYTOOLKIT-LICENSE.md",
                             "COMMUNITYTOOLKIT-THIRD-PARTY-NOTICES.txt"
                         })
                {
                    var path = Path.Combine(licenses, name);
                    Assert.IsTrue(File.Exists(path), $"Не скопирован {name}");
                    Assert.IsTrue(new FileInfo(path).Length > 200, $"Пустой {name}");
                }
            }
            finally
            {
                Directory.Delete(publish, true);
            }
        }

        [TestMethod]
        public void ThirdPartyNotice_NamesEveryShippedNuGetDependencyAndVersion()
        {
            var notice = File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md"));
            foreach (var project in new[]
                     {
                         Path.Combine(Root, "GCodeGenerator", "GCodeGenerator.csproj"),
                         Path.Combine(Root, "GCodeGenerator.Core", "GCodeGenerator.Core.csproj")
                     })
            {
                var document = System.Xml.Linq.XDocument.Load(project);
                foreach (var package in document.Descendants("PackageReference"))
                {
                    var name = package.Attribute("Include")?.Value;
                    var version = package.Attribute("Version")?.Value;
                    Assert.IsNotNull(name);
                    Assert.IsNotNull(version);
                    StringAssert.Contains(notice, $"{name} {version}",
                        $"В уведомлениях нет поставляемой зависимости {name} {version}");
                }
            }
        }
    }
}
