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
        public void ArcDirection_SelectsTheActuallyVisitedHalfCircle()
        {
            var profile = Settings().Machine;
            profile.MinX = -0.5;
            profile.MaxX = 2;

            var counterClockwise = ArcPath(clockwise: false, endX: 0, endY: 2, i: 0, j: 1);
            var clockwise = ArcPath(clockwise: true, endX: 0, endY: 2, i: 0, j: 1);

            Assert.AreEqual(0, MachineProfileValidator.Validate(
                counterClockwise, profile, System.Threading.CancellationToken.None).Count,
                "CCW-полукруг идёт через X=+1");
            Assert.IsTrue(MachineProfileValidator.Validate(
                    clockwise, profile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues)
                .Any(issue => issue.Property == "Machine.X" && issue.Code == ValidationCode.BelowMinimum),
                "CW-полукруг идёт через X=-1");
        }

        [TestMethod]
        public void PartialArc_DoesNotValidateTheUnvisitedPartOfTheCircle()
        {
            var profile = Settings().Machine;
            profile.MinX = -0.1;
            profile.MaxX = 1.1;
            profile.MinY = -0.1;
            profile.MaxY = 1.1;
            var quarter = ArcPath(
                clockwise: false,
                endX: 0,
                endY: 1,
                i: -1,
                j: 0,
                startX: 1,
                startY: 0);

            var failures = MachineProfileValidator.Validate(
                quarter, profile, System.Threading.CancellationToken.None);

            Assert.AreEqual(0, failures.Count,
                "Противоположные кардинальные точки не принадлежат четверти дуги");
        }

        [TestMethod]
        public void FullCircle_ValidatesEveryCardinalExtremum()
        {
            var profile = Settings().Machine;
            profile.MaxY = 0.5;
            var circle = ArcPath(
                clockwise: false,
                endX: 1,
                endY: 0,
                i: -1,
                j: 0,
                startX: 1,
                startY: 0);

            var failures = MachineProfileValidator.Validate(
                circle, profile, System.Threading.CancellationToken.None);

            Assert.IsTrue(failures.SelectMany(failure => failure.Issues)
                .Any(issue => issue.Property == "Machine.Y" && issue.Code == ValidationCode.AboveMaximum));
        }

        [TestMethod]
        public void CoordinateBounds_AreInclusiveOnEveryAxis()
        {
            var profile = Settings().Machine;
            var path = LinearPath(
                new ToolMove(ToolMoveKind.Rapid, x: profile.MinX, y: profile.MinY, z: profile.MinZ, feed: 100),
                new ToolMove(ToolMoveKind.Linear, x: profile.MaxX, y: profile.MaxY, z: profile.MaxZ, feed: 100));

            Assert.AreEqual(0, MachineProfileValidator.Validate(
                path, profile, System.Threading.CancellationToken.None).Count);
        }

        [TestMethod]
        public void CoordinateBounds_ReportLowerAndUpperViolationsOnEveryAxis()
        {
            var profile = Settings().Machine;
            var below = LinearPath(new ToolMove(
                ToolMoveKind.Rapid,
                x: profile.MinX - 0.1,
                y: profile.MinY - 0.1,
                z: profile.MinZ - 0.1,
                feed: 100));
            var above = LinearPath(new ToolMove(
                ToolMoveKind.Rapid,
                x: profile.MaxX + 0.1,
                y: profile.MaxY + 0.1,
                z: profile.MaxZ + 0.1,
                feed: 100));

            var lowIssues = MachineProfileValidator.Validate(
                below, profile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues).ToArray();
            var highIssues = MachineProfileValidator.Validate(
                above, profile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues).ToArray();

            foreach (var axis in new[] { "Machine.X", "Machine.Y", "Machine.Z" })
            {
                Assert.IsTrue(lowIssues.Any(issue =>
                    issue.Property == axis && issue.Code == ValidationCode.BelowMinimum));
                Assert.IsTrue(highIssues.Any(issue =>
                    issue.Property == axis && issue.Code == ValidationCode.AboveMaximum));
            }
        }

        [TestMethod]
        public void WorkAndRapidFeed_HaveSeparateInclusiveLimits()
        {
            var profile = Settings().Machine;
            profile.MaxWorkFeed = 200;
            profile.MaxRapidFeed = 500;
            var exact = LinearPath(
                new ToolMove(ToolMoveKind.Rapid, x: 0, feed: 500),
                new ToolMove(ToolMoveKind.Linear, x: 1, feed: 200));
            var excessive = LinearPath(
                new ToolMove(ToolMoveKind.Rapid, x: 0, feed: 501),
                new ToolMove(ToolMoveKind.Linear, x: 1, feed: 201));

            Assert.AreEqual(0, MachineProfileValidator.Validate(
                exact, profile, System.Threading.CancellationToken.None).Count);
            var issues = MachineProfileValidator.Validate(
                    excessive, profile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues).ToArray();
            Assert.IsTrue(issues.Any(issue => issue.Property == "Machine.RapidFeed" && issue.Limit == 500));
            Assert.IsTrue(issues.Any(issue => issue.Property == "Machine.WorkFeed" && issue.Limit == 200));
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
        public void EveryInvalidMachineProfileField_IsNamed()
        {
            var settings = Settings();
            settings.Machine.MinX = double.NaN;
            settings.Machine.MaxX = double.PositiveInfinity;
            settings.Machine.MinY = 1;
            settings.Machine.MaxY = 1;
            settings.Machine.MinZ = 2;
            settings.Machine.MaxZ = -2;
            settings.Machine.MaxWorkFeed = 0;
            settings.Machine.MaxRapidFeed = OperationValidation.MaxRapidFeed + 1;
            settings.Machine.MaxSpindleSpeedRpm = 0;

            var issues = GCodeSettingsValidation.Validate(settings);
            var properties = issues.Select(issue => issue.Property).ToArray();

            foreach (var property in new[]
                     {
                         "MinX", "MaxX", "MaxY", "MaxZ", "MaxWorkFeed",
                         "MaxRapidFeed", "MaxSpindleSpeedRpm",
                     })
            {
                Assert.IsTrue(properties.Contains(property), $"Не названо поле {property}");
            }
        }

        [TestMethod]
        public void StartAndEndCoordinates_AreCheckedAgainstTheProfile()
        {
            var settings = Settings();
            settings.WorkCoordinate.AddStartPosition = true;
            settings.WorkCoordinate.StartX = settings.Machine.MinX - 1;
            settings.WorkCoordinate.StartY = settings.Machine.MaxY + 1;
            settings.WorkCoordinate.StartZ = settings.Machine.MinZ - 1;
            settings.WorkCoordinate.AddEndPosition = true;
            settings.WorkCoordinate.EndX = settings.Machine.MaxX + 1;
            settings.WorkCoordinate.EndY = settings.Machine.MinY - 1;
            settings.WorkCoordinate.EndZ = settings.Machine.MaxZ + 1;

            var issues = GCodeSettingsValidation.Validate(settings);

            foreach (var property in new[] { "StartX", "StartY", "StartZ", "EndX", "EndY", "EndZ" })
                Assert.IsTrue(issues.Any(issue => issue.Property == property), property);
        }

        [TestMethod]
        public void DisabledMachineProfile_IgnoresUnconfiguredPlaceholderValues()
        {
            var settings = Settings();
            settings.Machine.Enabled = false;
            settings.Machine.MinX = double.NaN;
            settings.Machine.MaxWorkFeed = -1;
            settings.Machine.MaxSpindleSpeedRpm = -1;

            Assert.AreEqual(0, GCodeSettingsValidation.Validate(settings).Count);
        }

        [TestMethod]
        public void MissingMachineProfile_IsRejected()
        {
            var settings = Settings();
            settings.Machine = null!;

            var issues = GCodeSettingsValidation.Validate(settings);

            Assert.IsTrue(issues.Any(issue => issue.Property == "Machine"));
        }

        [TestMethod]
        public void SpindleLimit_AppliesOnlyToAnEmittedSpeedAndIncludesEquality()
        {
            var settings = Settings();
            settings.Machine.MaxSpindleSpeedRpm = 10000;
            settings.Spindle.SpindleSpeedRpm = 10000;
            Assert.IsFalse(GCodeSettingsValidation.Validate(settings)
                .Any(issue => issue.Property == "SpindleSpeedRpm"));

            settings.Spindle.SpindleSpeedRpm = 10001;
            settings.Spindle.SpindleSpeedEnabled = false;
            Assert.IsFalse(GCodeSettingsValidation.Validate(settings)
                .Any(issue => issue.Property == "SpindleSpeedRpm"));

            settings.Spindle.SpindleSpeedEnabled = true;
            Assert.IsTrue(GCodeSettingsValidation.Validate(settings)
                .Any(issue => issue.Property == "SpindleSpeedRpm" && issue.Limit == 10000));
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

        private static ToolPath ArcPath(
            bool clockwise,
            double endX,
            double endY,
            double i,
            double j,
            double startX = 0,
            double startY = 0)
        {
            var path = new ToolPath();
            var operation = new ToolPathOperation("Arc", "Arc", 3, new object());
            path.AddOperation(operation);
            var builder = new ToolPathBuilder(operation);
            builder.RapidTo(x: startX, y: startY, feed: 100);
            if (clockwise)
                builder.ArcCW(endX, endY, i, j, feed: 100);
            else
                builder.ArcCCW(endX, endY, i, j, feed: 100);
            return path;
        }

        private static ToolPath LinearPath(params ToolMove[] moves)
        {
            var path = new ToolPath();
            var operation = new ToolPathOperation("Moves", "Moves", 3, new object());
            path.AddOperation(operation);
            foreach (var move in moves)
                operation.Add(move);
            return path;
        }
    }
}
