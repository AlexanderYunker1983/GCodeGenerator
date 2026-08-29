using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Локальный паспорт станка проверяет фактическую траекторию.</summary>
    [TestClass]
    public class MachineProfileTests
    {
        [TestMethod]
        public void DisabledProfile_DoesNotRestrictExistingProjects()
        {
            var settings = Settings();
            settings.Machine.Enabled = false;
            settings.Machine.MaxX = 0.5;

            var path = new SimpleGCodeGenerator().BuildToolPath(OneDrill(x: 10), settings);

            Assert.IsFalse(path.IsEmpty);
        }

        [TestMethod]
        public void CoordinateOutsideEnvelope_RejectsTheSourceOperation()
        {
            var settings = Settings();

            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().BuildToolPath(OneDrill(x: 11), settings));

            Assert.AreEqual(1, error.Failures.Count);
            Assert.AreEqual(0, error.Failures[0].OperationIndex);
            Assert.IsTrue(error.Failures[0].Issues.Any(issue => issue.Property == "Machine.X"));
        }

        [TestMethod]
        public void FeedAboveMachineRating_IsRejectedAfterToolPathBuild()
        {
            var settings = Settings();
            settings.Machine.MaxWorkFeed = 100;

            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().BuildToolPath(OneDrill(x: 1), settings));

            Assert.IsTrue(error.Failures.SelectMany(failure => failure.Issues)
                .Any(issue => issue.Property == "Machine.WorkFeed" && issue.Limit == 100));
        }

        [TestMethod]
        public void ArcIntermediateExtremum_IsCheckedNotOnlyItsEndpoints()
        {
            var path = new ToolPath();
            var operation = new ToolPathOperation("Arc", "Arc", 3, new object(), sourceIndex: 3);
            path.AddOperation(operation);
            var builder = new ToolPathBuilder(operation);
            builder.RapidTo(x: 0, y: 0, feed: 100);
            // Endpoints have X=0, but the CCW semicircle reaches X=1.
            builder.ArcCCW(x: 0, y: 2, i: 0, j: 1, feed: 100);
            var profile = Settings().Machine;
            profile.MaxX = 0.5;

            var failures = MachineProfileValidator.Validate(
                path, profile, System.Threading.CancellationToken.None);

            Assert.AreEqual(1, failures.Count);
            Assert.AreEqual(3, failures[0].OperationIndex);
            Assert.IsTrue(failures[0].Issues.Any(issue => issue.Property == "Machine.X"));
        }

        [TestMethod]
        public void InvalidProfileAndSpindleRating_AreRejectedAsSettings()
        {
            var settings = Settings();
            settings.Machine.MinX = 5;
            settings.Machine.MaxX = 5;
            settings.Machine.MaxSpindleSpeedRpm = 10000;
            settings.Spindle.SpindleSpeedRpm = 12000;

            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().BuildToolPath(OneDrill(x: 1), settings));

            Assert.IsTrue(error.SettingsIssues.Any(issue => issue.Property == "MaxX"));
            Assert.IsTrue(error.SettingsIssues.Any(issue =>
                issue.Property == "SpindleSpeedRpm" && issue.Limit == 10000));
        }

        [TestMethod]
        public void ProjectFile_DoesNotCarryTheLocalMachineProfile()
        {
            var settings = Settings();
            settings.Machine.MaxX = 1;

            var json = new ProjectFileService().Serialize(OneDrill(x: 1), settings);

            Assert.IsFalse(json.Contains("\"machine\""),
                "Чужой проект не должен подменять локальные пределы станка");
        }

        private static GCodeSettings Settings()
        {
            var settings = new GCodeSettings();
            settings.Machine.Enabled = true;
            settings.Machine.MinX = -10;
            settings.Machine.MaxX = 10;
            settings.Machine.MinY = -10;
            settings.Machine.MaxY = 10;
            settings.Machine.MinZ = -10;
            settings.Machine.MaxZ = 10;
            settings.Machine.MaxWorkFeed = 1000;
            settings.Machine.MaxRapidFeed = 2000;
            settings.Machine.MaxSpindleSpeedRpm = 24000;
            return settings;
        }

        private static List<OperationBase> OneDrill(double x)
            => new List<OperationBase>
            {
                new DrillPointsOperation
                {
                    Name = "Drill",
                    Holes =
                    {
                        new DrillHole
                        {
                            X = x,
                            Y = 1,
                            Z = 0,
                            TotalDepth = 2,
                            StepDepth = 1
                        }
                    }
                }
            };
    }
}
