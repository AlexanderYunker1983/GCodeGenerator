using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Порог покрытия должен быть исполняемым условием, а не отчётом.</summary>
    [TestClass]
    public class CoverageGateTests
    {
        private static string Root => RepositoryRootLocator.Find();

        [TestMethod]
        public void Gate_PassesOnlyWhenBothRatesReachTheirThresholds()
        {
            var passing = WriteReport("passing", 0.80, 0.65);
            var lowLines = WriteReport("low-lines", 0.7999, 0.90);
            var lowBranches = WriteReport("low-branches", 0.90, 0.6499);
            try
            {
                Assert.AreEqual(0, RunGate(passing).ExitCode);

                var lineFailure = RunGate(lowLines);
                Assert.AreNotEqual(0, lineFailure.ExitCode);
                StringAssert.Contains(lineFailure.Output, "line coverage");

                var branchFailure = RunGate(lowBranches);
                Assert.AreNotEqual(0, branchFailure.ExitCode);
                StringAssert.Contains(branchFailure.Output, "branch coverage");
            }
            finally
            {
                Directory.Delete(passing, true);
                Directory.Delete(lowLines, true);
                Directory.Delete(lowBranches, true);
            }
        }

        [TestMethod]
        public void Gate_FailsClosedWhenReportOrProductPackageIsMissing()
        {
            var empty = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(empty);
            var wrongPackage = WriteReport("wrong-package", 1, 1, "GCodeGenerator.Tests");
            try
            {
                Assert.AreNotEqual(0, RunGate(empty).ExitCode);

                var missingProduct = RunGate(wrongPackage);
                Assert.AreNotEqual(0, missingProduct.ExitCode);
                StringAssert.Contains(missingProduct.Output, "GCodeGenerator' package");
            }
            finally
            {
                Directory.Delete(empty, true);
                Directory.Delete(wrongPackage, true);
            }
        }

        [TestMethod]
        public void Gate_AcceptsIdenticalCollectorCopiesButRejectsDifferentReports()
        {
            var identical = WriteReport("identical-copies", 0.80, 0.65);
            var nestedDirectory = Path.Combine(identical, "In", "test-host");
            Directory.CreateDirectory(nestedDirectory);
            File.Copy(
                Path.Combine(identical, "coverage.cobertura.xml"),
                Path.Combine(nestedDirectory, "coverage.cobertura.xml"));

            var different = WriteReport("different-reports", 0.80, 0.65);
            var secondDirectory = Path.Combine(different, "In", "test-host");
            Directory.CreateDirectory(secondDirectory);
            File.WriteAllText(
                Path.Combine(secondDirectory, "coverage.cobertura.xml"),
                "<coverage><packages><package name=\"GCodeGenerator\" line-rate=\"0.90\" branch-rate=\"0.75\" /></packages></coverage>");

            try
            {
                Assert.AreEqual(0, RunGate(identical).ExitCode);

                var result = RunGate(different);
                Assert.AreNotEqual(0, result.ExitCode);
                StringAssert.Contains(result.Output, "exactly one distinct Cobertura report");
            }
            finally
            {
                Directory.Delete(identical, true);
                Directory.Delete(different, true);
            }
        }

        [TestMethod]
        public void Ci_CollectsCoberturaAndEnforcesBothProductAssemblies()
        {
            var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "ci.yml"));

            StringAssert.Contains(workflow, "Code Coverage;Format=cobertura");
            StringAssert.Contains(workflow, "build/Assert-Coverage.ps1");
            StringAssert.Contains(workflow, "-Assembly GCodeGenerator.Core");
            StringAssert.Contains(workflow, "-Assembly GCodeGenerator");
            StringAssert.Contains(workflow, "-MinimumLinePercent 80");
            StringAssert.Contains(workflow, "-MinimumBranchPercent 65");
        }

        private static string WriteReport(
            string name,
            double lineRate,
            double branchRate,
            string package = "GCodeGenerator")
        {
            var directory = Path.Combine(Path.GetTempPath(), $"gcodegen-coverage-{name}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var xml = FormattableString.Invariant(
                $"<coverage><packages><package name=\"{package}\" line-rate=\"{lineRate}\" branch-rate=\"{branchRate}\" /></packages></coverage>");
            File.WriteAllText(Path.Combine(directory, "coverage.cobertura.xml"), xml);
            return directory;
        }

        private static (int ExitCode, string Output) RunGate(string resultsDirectory)
        {
            var script = Path.Combine(Root, "build", "Assert-Coverage.ps1");
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
                         "-File", script,
                         "-ResultsDirectory", resultsDirectory,
                         "-Assembly", "GCodeGenerator",
                         "-MinimumLinePercent", "80",
                         "-MinimumBranchPercent", "65"
                     })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(30000);
            return (process.ExitCode, stdout + stderr);
        }
    }
}
