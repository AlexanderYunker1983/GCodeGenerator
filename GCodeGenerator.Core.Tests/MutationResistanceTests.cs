#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Граничные утверждения для условий и формул релизно-критичного ядра.
    /// Они намеренно проверяют точные результаты, а не только наличие ошибки.
    /// </summary>
    [TestClass]
    public sealed class MutationResistanceTests
    {
        [TestMethod]
        public void PolylineBudgets_AreInclusiveAndCountEveryNonNullPolyline()
        {
            var issues = new List<ValidationIssue>();
            var exactContours = Enumerable.Range(0, GenerationLimits.MaxImportedContoursPerOperation)
                .Select(_ => new Polyline2D()).ToList();
            Assert.IsTrue(OperationValidation.AddPolylineComplexityIssues(
                issues, "Geometry", exactContours));
            Assert.AreEqual(0, issues.Count);

            exactContours.Add(new Polyline2D());
            Assert.IsFalse(OperationValidation.AddPolylineComplexityIssues(
                issues, "Geometry", exactContours));
            Assert.AreEqual(
                $"must contain at most {GenerationLimits.MaxImportedContoursPerOperation} contours, but contains {exactContours.Count}",
                issues.Single().Message);

            issues.Clear();
            var point = new Point2D { X = 1, Y = 2 };
            var oversizedPoints = new List<Point2D>(
                Enumerable.Repeat(point, GenerationLimits.MaxImportedPointsPerOperation + 1));
            var polylines = new List<Polyline2D> { null!, new Polyline2D { Points = oversizedPoints } };
            Assert.IsFalse(OperationValidation.AddPolylineComplexityIssues(
                issues, "Geometry", polylines));
            Assert.AreEqual(
                $"must contain at most {GenerationLimits.MaxImportedPointsPerOperation} points, but contains {oversizedPoints.Count}",
                issues.Single().Message);
        }

        [TestMethod]
        public void PolylineCoordinates_ReportBothAxesWithTheirExactPaths()
        {
            var issues = new List<ValidationIssue>();
            var polylines = new List<Polyline2D>
            {
                new Polyline2D
                {
                    Points = new List<Point2D>
                    {
                        new Point2D { X = double.NaN, Y = double.PositiveInfinity }
                    }
                }
            };

            OperationValidation.AddPolylinePointIssues(issues, "Contours", polylines);

            CollectionAssert.AreEqual(
                new[] { "Contours[0].Points[0].X", "Contours[0].Points[0].Y" },
                issues.Select(issue => issue.Property).ToArray());
            Assert.IsTrue(issues.All(issue => issue.Code == ValidationCode.NotFinite));
        }

        [TestMethod]
        public void HelicalEntry_RequiresEveryFinitePositiveInputAndUsesTheTurnFormula()
        {
            var operation = OperationFixtures.PocketCircle();
            operation.EntryMode = PocketEntryMode.Helical;
            operation.TotalDepth = 10;
            operation.StepDepth = 10;
            operation.RetractHeight = 0.3;
            operation.HelicalEntryDiameter = 1;
            var layerDepth = Math.Min(operation.TotalDepth, operation.StepDepth) + operation.RetractHeight;
            var boundaryRadians = Math.Atan(
                layerDepth / (Math.PI * operation.HelicalEntryDiameter
                              * PocketOperationBase.MaxHelicalEntryTurnsPerLayer));
            operation.EntryAngle = boundaryRadians * 180.0 / Math.PI;

            Assert.IsFalse(operation.Validate().Any(issue => issue.Code == ValidationCode.Inconsistent),
                "Ровно допустимое число витков проходит");

            operation.EntryAngle *= 0.999;
            var turnIssue = operation.Validate().Single(issue => issue.Code == ValidationCode.Inconsistent);
            Assert.AreEqual(nameof(operation.EntryAngle), turnIssue.Property);
            StringAssert.Contains(turnIssue.Message, "turns per layer; at most 1000 are allowed");

            foreach (var invalid in new Action<PocketCircleOperation>[]
                     {
                         item => item.EntryAngle = double.NaN,
                         item => item.HelicalEntryDiameter = 0,
                         item => item.TotalDepth = 0,
                         item => item.StepDepth = 0,
                     })
            {
                var candidate = OperationFixtures.PocketCircle();
                candidate.EntryMode = PocketEntryMode.Helical;
                invalid(candidate);
                Assert.IsFalse(candidate.Validate().Any(issue => issue.Code == ValidationCode.Inconsistent),
                    "Формула витков не вычисляется на уже отвергнутых входах");
            }
        }

        [TestMethod]
        public void MachineProfile_ChecksBottomAndLeftFullCircleExtremaWithExactDiagnostics()
        {
            var circle = ArcPath(
                clockwise: false,
                startX: 5,
                startY: 7,
                endX: 5,
                endY: 7,
                i: -1,
                j: 0);

            var leftProfile = Profile();
            leftProfile.MinX = 3.5;
            var left = MachineProfileValidator.Validate(
                circle, leftProfile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues).Single();
            Assert.AreEqual("Machine.X", left.Property);
            Assert.AreEqual("machine-profile minimum is 3.5 mm, but the tool path reaches 3 mm", left.Message);

            var bottomProfile = Profile();
            bottomProfile.MinY = 6.5;
            var bottom = MachineProfileValidator.Validate(
                circle, bottomProfile, System.Threading.CancellationToken.None)
                .SelectMany(failure => failure.Issues).Single();
            Assert.AreEqual("Machine.Y", bottom.Property);
            Assert.AreEqual("machine-profile minimum is 6.5 mm, but the tool path reaches 6 mm", bottom.Message);
        }

        [TestMethod]
        public void MachineProfile_HandlesClockwiseArcAcrossZeroWithoutCheckingUnvisitedQuadrants()
        {
            var arc = ArcPath(
                clockwise: true,
                startX: 1,
                startY: 0,
                endX: 0,
                endY: -1,
                i: -1,
                j: 0);
            var profile = Profile();
            profile.MinX = -0.1;
            profile.MaxX = 1.1;
            profile.MinY = -1.1;
            profile.MaxY = 0.1;

            Assert.AreEqual(0, MachineProfileValidator.Validate(
                arc, profile, System.Threading.CancellationToken.None).Count);
        }

        [TestMethod]
        public void SettingsValidation_KeepsInclusiveCoordinatesAndExactRangeDiagnostics()
        {
            var settings = new GCodeSettings();
            settings.Machine.Enabled = true;
            settings.Machine.MinX = -10;
            settings.Machine.MaxX = 10;
            settings.Machine.MinY = double.NaN;
            settings.Machine.MaxY = 10;
            settings.Machine.MinZ = -10;
            settings.Machine.MaxZ = double.PositiveInfinity;
            settings.WorkCoordinate.AddStartPosition = true;
            settings.WorkCoordinate.StartX = -10;
            settings.WorkCoordinate.StartY = 0;
            settings.WorkCoordinate.StartZ = 0;
            settings.WorkCoordinate.AddEndPosition = true;
            settings.WorkCoordinate.EndX = 11;

            var issues = GCodeSettingsValidation.Validate(settings);

            Assert.IsFalse(issues.Any(issue => issue.Property == nameof(WorkCoordinateSettings.StartX)));
            var end = issues.Single(issue => issue.Property == nameof(WorkCoordinateSettings.EndX));
            Assert.AreEqual(ValidationCode.AboveMaximum, end.Code);
            Assert.AreEqual("must be at most the machine-profile limit 10, but is 11", end.Message);
            Assert.AreEqual(1, issues.Count(issue => issue.Property == nameof(MachineProfileSettings.MinY)));
            Assert.AreEqual(1, issues.Count(issue => issue.Property == nameof(MachineProfileSettings.MaxZ)));
            Assert.IsFalse(issues.Any(issue => issue.Code == ValidationCode.Inconsistent));
        }

        [TestMethod]
        public void DisabledMachineProfile_StillValidatesAllEnabledStartCoordinates()
        {
            var settings = new GCodeSettings();
            settings.Machine.Enabled = false;
            settings.WorkCoordinate.AddStartPosition = true;
            settings.WorkCoordinate.StartX = double.NaN;
            settings.WorkCoordinate.StartY = double.NegativeInfinity;
            settings.WorkCoordinate.StartZ = double.PositiveInfinity;

            var issues = GCodeSettingsValidation.Validate(settings);

            CollectionAssert.AreEquivalent(
                new[] { "StartX", "StartY", "StartZ" },
                issues.Select(issue => issue.Property).ToArray());
        }

        [TestMethod]
        public void EnumValidation_DoesNotDuplicateAnExistingPropertyIssue()
        {
            var issues = new List<ValidationIssue>
            {
                new ValidationIssue("Other", "other"),
                new ValidationIssue("Mode", "already invalid")
            };

            EnumValidation.AddIfUndefined(issues, "Mode", (PocketMode)99);

            Assert.AreEqual(2, issues.Count);
            Assert.AreEqual(1, issues.Count(issue => issue.Property == "Mode"));
        }

        [TestMethod]
        public void GenericPostProcessor_DoesNotEmitDisabledSpeedOrZeroDelay()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleSpeedEnabled = false;
            settings.Spindle.SpindleSpeedRpm = 12345;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = 0;

            var lines = new GenericPostProcessor().Build(new ToolPath(), settings).Lines;

            Assert.IsTrue(lines.Any(line => line.EndsWith("M3", StringComparison.Ordinal)));
            Assert.IsFalse(lines.Any(line => line.Contains("S12345", StringComparison.Ordinal)));
            Assert.IsFalse(lines.Any(line => line.Contains("G4 P", StringComparison.Ordinal)));
        }

        [TestMethod]
        public void ProjectReader_DuplicateNestedFieldNamesItsExactArrayIndex()
        {
            const string json = "{\"version\":4,\"operations\":[{\"type\":\"ProfileDxf\",\"data\":{"
                + "\"Polylines\":[{\"Points\":[{\"X\":0,\"Y\":0},{\"X\":1,\"Y\":1,\"Y\":2}]}]}}]}";

            var failure = Assert.Throws<CoreException>(() => new ProjectFileService().Deserialize(json));

            Assert.AreEqual(CoreErrorCodes.ProjectFileCorrupt, failure.Code);
            Assert.AreEqual(
                "The project file is damaged or has an unexpected structure "
                + "(operation [0] (ProfileDxf).Polylines[0].Points[1] field 'Y' occurs more than once).",
                failure.Message);
        }

        [TestMethod]
        public void ProjectReader_NonObjectSectionNamesTheSectionAndActualShape()
        {
            var failure = Assert.Throws<CoreException>(() =>
                new ProjectFileService().Deserialize("{\"version\":4,\"operations\":[],\"spindle\":42}"));

            Assert.AreEqual(
                "The project file is damaged or has an unexpected structure "
                + "(the section 'spindle' is not a JSON object).",
                failure.Message);
        }

        private static MachineProfileSettings Profile()
            => new MachineProfileSettings
            {
                Enabled = true,
                MinX = -100,
                MaxX = 100,
                MinY = -100,
                MaxY = 100,
                MinZ = -100,
                MaxZ = 100,
                MaxWorkFeed = 1000,
                MaxRapidFeed = 2000,
                MaxSpindleSpeedRpm = 24000
            };

        private static ToolPath ArcPath(
            bool clockwise,
            double startX,
            double startY,
            double endX,
            double endY,
            double i,
            double j)
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
    }
}
