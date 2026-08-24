using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Версионирование из git-тегов: build/Get-GitVersion.ps1 (выбор тега и
    /// приоритет нескольких тегов на одном коммите) и версия, проставленная в
    /// сборку GCodeGenerator (формат X.Y.Z[-suffix], численная часть == тег).
    /// </summary>
    [TestClass]
    public class VersioningTests
    {
        private static string _root;

        [ClassInitialize]
        public static void Init(TestContext _)
        {
            _root = Path.Combine(Path.GetTempPath(), "gcodegen_version_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            try { Directory.Delete(_root, true); } catch { /* best effort */ }
        }

        private static string RunGit(string workDir, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = workDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return stdout.Trim();
        }

        private static string NewRepo(string name)
        {
            var dir = Path.Combine(_root, name);
            Directory.CreateDirectory(dir);
            RunGit(dir, "init -q");
            RunGit(dir, "config user.email test@example.com");
            RunGit(dir, "config user.name Test");
            RunGit(dir, "config commit.gpgsign false");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "a");
            RunGit(dir, "add a.txt");
            RunGit(dir, "commit -q -m one");
            return dir;
        }

        private static void Commit(string dir, string file, string content)
        {
            File.WriteAllText(Path.Combine(dir, file), content);
            RunGit(dir, $"add {file}");
            RunGit(dir, $"commit -q -m {file}");
        }

        /// <summary>Запускает build/Get-GitVersion.ps1 в рабочем каталоге = dir.</summary>
        private static string RunVersionScript(string dir)
        {
            var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "build", "Get-GitVersion.ps1");
            Assert.IsTrue(File.Exists(script), $"Скрипт не скопирован в вывод тестов: {script}");

            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"")
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, $"Скрипт завершился с ошибкой. stderr: {stderr}");
            return stdout.Trim();
        }

        private static void CheckSelection(string name, string[] tags, string expected)
        {
            var dir = NewRepo(name);
            foreach (var tag in tags)
                RunGit(dir, $"tag {tag}");
            var actual = RunVersionScript(dir);
            Assert.AreEqual(expected, actual, $"Теги: [{string.Join(", ", tags)}]");
        }

        [TestMethod]
        public void NoTags_DefaultVersion()
        {
            CheckSelection("notags", Array.Empty<string>(), "0.1.0-alpha");
        }

        [TestMethod]
        public void SingleTagOnHead_Used()
        {
            CheckSelection("single", new[] { "1.2.3" }, "1.2.3");
        }

        [TestMethod]
        public void MultipleTagsOnHead_ReleaseWins()
        {
            CheckSelection("multi-release",
                new[] { "1.2.3-alpha", "1.2.3-beta", "1.2.3-rc5", "1.2.3" }, "1.2.3");
        }

        [TestMethod]
        public void MultipleTagsOnHead_HighestPrereleaseWins()
        {
            CheckSelection("multi-prerelease",
                new[] { "1.2.3-alpha", "1.2.3-alpha2", "1.2.3-beta", "1.2.3-beta3", "1.2.3-rc5" },
                "1.2.3-rc5");
        }

        [TestMethod]
        public void PrereleaseNumber_OrdinalComparison()
        {
            // rc10 > rc5 — числовое сравнение, а не лексикографическое.
            CheckSelection("rc-numeric", new[] { "1.2.3-rc5", "1.2.3-rc10" }, "1.2.3-rc10");
        }

        [TestMethod]
        public void PrereleaseClass_BeatsNumber()
        {
            // beta > alpha2 — класс важнее номера.
            CheckSelection("class-beats-number", new[] { "1.2.3-alpha2", "1.2.3-beta" }, "1.2.3-beta");
        }

        [TestMethod]
        public void BaseVersion_BeatsPrerelease()
        {
            // 2.0.0-alpha > 1.9.9 — базовая версия важнее суффикса.
            CheckSelection("base-beats-prerelease", new[] { "1.9.9", "2.0.0-alpha" }, "2.0.0-alpha");
        }

        [TestMethod]
        public void InvalidTags_Ignored()
        {
            CheckSelection("invalid-only", new[] { "v1.2.3", "1.2", "foo" }, "0.1.0-alpha");
        }

        [TestMethod]
        public void InvalidAndValidTags_ValidWins()
        {
            CheckSelection("mixed", new[] { "v1.2.3", "1.2.3-rc5" }, "1.2.3-rc5");
        }

        [TestMethod]
        public void TagOnOlderCommit_NearestInHistoryUsed()
        {
            var dir = NewRepo("old-tag");
            RunGit(dir, "tag 0.5.0");
            Commit(dir, "b.txt", "b"); // HEAD без тега
            Assert.AreEqual("0.5.0", RunVersionScript(dir));
        }

        [TestMethod]
        public void GCodeGeneratorAssembly_VersionMatchesTagFormat()
        {
            var asm = typeof(MainViewModel).Assembly;
            var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Assert.IsFalse(string.IsNullOrEmpty(informational), "InformationalVersion проставлен");
            Assert.IsTrue(Regex.IsMatch(informational, @"^\d+\.\d+\.\d+(-[A-Za-z][A-Za-z0-9]*)?$"),
                $"InformationalVersion не в формате X.Y.Z[-suffix]: {informational}");

            // Численная часть AssemblyVersion == численная часть тега.
            var numeric = asm.GetName().Version;
            var parts = informational.Split('-')[0].Split('.');
            Assert.AreEqual(int.Parse(parts[0]), numeric.Major);
            Assert.AreEqual(int.Parse(parts[1]), numeric.Minor);
            Assert.AreEqual(int.Parse(parts[2]), numeric.Build);
        }
    }
}
