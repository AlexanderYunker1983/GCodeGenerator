using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Mutation CI обязан быть закреплённым и fail-closed.</summary>
    [TestClass]
    public class MutationConfigurationTests
    {
        private static string Root => RepositoryRootLocator.Find();

        [TestMethod]
        public void ToolAndSafetyCriticalScope_ArePinned()
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(Root, ".config", "dotnet-tools.json")));
            var tool = manifest.RootElement.GetProperty("tools").GetProperty("dotnet-stryker");
            Assert.AreEqual("4.16.0", tool.GetProperty("version").GetString());

            using var config = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(Root, "GCodeGenerator.Core.Tests", "stryker-config.json")));
            var stryker = config.RootElement.GetProperty("stryker-config");
            var mutate = stryker.GetProperty("mutate")
                .EnumerateArray().Select(item => item.GetString()).ToArray();

            foreach (var criticalFile in new[]
                     {
                         "GenericPostProcessor.cs",
                         "MachineProfileValidator.cs",
                         "GCodeSettingsValidation.cs",
                         "OperationValidation.cs",
                         "ProjectFileReader.cs",
                     })
            {
                Assert.IsTrue(mutate.Any(path => path!.EndsWith(criticalFile, StringComparison.Ordinal)),
                    $"Mutation scope не содержит {criticalFile}");
            }

            Assert.IsTrue(stryker.GetProperty("thresholds").GetProperty("break").GetInt32() >= 70,
                "Низкий mutation score должен останавливать workflow");
            Assert.IsTrue(stryker.GetProperty("break-on-initial-test-failure").GetBoolean());
        }

        [TestMethod]
        public void Workflow_RunsRegularlyAndKeepsReportEvenOnFailure()
        {
            var workflow = File.ReadAllText(
                Path.Combine(Root, ".github", "workflows", "mutation.yml"));

            StringAssert.Contains(workflow, "workflow_dispatch:");
            StringAssert.Contains(workflow, "schedule:");
            StringAssert.Contains(workflow, "dotnet restore GCodeGenerator.sln --locked-mode");
            StringAssert.Contains(workflow, "dotnet tool restore");
            StringAssert.Contains(workflow, "dotnet stryker --config-file stryker-config.json --skip-version-check");
            StringAssert.Contains(workflow, "if: always()");
            StringAssert.Contains(workflow, "GCodeGenerator.Core.Tests/StrykerOutput");
        }
    }
}
