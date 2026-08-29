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

            Assert.IsTrue(stryker.GetProperty("thresholds").GetProperty("break").GetInt32() >= 75,
                "Низкий mutation score должен останавливать workflow");
            Assert.IsTrue(stryker.GetProperty("break-on-initial-test-failure").GetBoolean());
        }

        [TestMethod]
        public void WeeklyScope_CoversTrajectoryEmittersGeometryAndDxfImport()
        {
            using var config = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(Root, "GCodeGenerator.Core.Tests", "stryker-weekly-config.json")));
            var stryker = config.RootElement.GetProperty("stryker-config");
            var mutate = stryker.GetProperty("mutate")
                .EnumerateArray().Select(item => item.GetString()!).ToArray();

            foreach (var criticalFile in new[]
                     {
                         "DrillPointsOperationGenerator.cs",
                         "UnifiedPocketGenerator.cs",
                         "UnifiedProfileGenerator.cs",
                         "ProgramBuilder.cs",
                         "GCodeFormatter.cs",
                         "ConcentricPocketingStrategy.cs",
                         "LinesPocketingStrategy.cs",
                         "RadialPocketingStrategy.cs",
                         "SpiralPocketingStrategy.cs",
                         "ZigZagPocketingStrategy.cs",
                         "Geometry2D.cs",
                         "ContourOffset.cs",
                         "DxfEntityReader.cs",
                         "DxfClosedContourBuilder.cs",
                     })
            {
                Assert.IsTrue(mutate.Any(path => path.EndsWith(criticalFile, StringComparison.Ordinal)),
                    $"Расширенный mutation scope не содержит {criticalFile}");
            }

            Assert.IsTrue(stryker.GetProperty("thresholds").GetProperty("break").GetInt32() >= 75);
            Assert.IsTrue(stryker.GetProperty("break-on-initial-test-failure").GetBoolean());
        }

        [TestMethod]
        public void Workflow_RunsRegularlyAndKeepsReportEvenOnFailure()
        {
            var workflow = File.ReadAllText(
                Path.Combine(Root, ".github", "workflows", "mutation.yml"));

            StringAssert.Contains(workflow, "workflow_dispatch:");
            StringAssert.Contains(workflow, "pull_request:",
                "Деградация mutation score обнаруживается только через неделю");
            StringAssert.Contains(workflow, "schedule:");
            StringAssert.Contains(workflow, "dotnet restore GCodeGenerator.sln --locked-mode");
            StringAssert.Contains(workflow, "dotnet tool restore");
            StringAssert.Contains(workflow,
                "dotnet stryker --config-file stryker-weekly-config.json --skip-version-check");
            StringAssert.Contains(workflow, "if: always()");
            StringAssert.Contains(workflow, "GCodeGenerator.Core.Tests/StrykerOutput");
            StringAssert.Contains(workflow, "timeout-minutes: 120",
                "Расширенный недельный mutation-прогон не должен обрываться на прежних 45 минутах");
        }
    }
}
