using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            var ignored = Path.Combine(directory, "release-notes.md");
            File.WriteAllBytes(installer, new byte[] { 0, 1, 2, 3 });
            File.WriteAllBytes(portable, new byte[] { 9, 8, 7 });
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
                var expected = new[] { portable, installer }
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
    }
}
