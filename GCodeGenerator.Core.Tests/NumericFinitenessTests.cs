#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Нечисловые значения не должны добираться до управляющей программы:
    /// JSON допускает именованные NaN/Infinity, а программный API может
    /// присвоить их напрямую, минуя ограничения текстового поля.
    /// </summary>
    [TestClass]
    public sealed class NumericFinitenessTests
    {
        [TestMethod]
        public void EveryPublicDoublePropertyOfEveryOperation_RejectsInfinity()
        {
            var failures = new List<string>();

            foreach (var descriptor in OperationCatalog.All)
            {
                foreach (var property in descriptor.OperationType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                             .Where(property => property.PropertyType == typeof(double) && property.CanWrite))
                {
                    var operation = descriptor.Create();
                    property.SetValue(operation, double.PositiveInfinity);

                    var issues = ((IValidatable)operation).Validate();
                    if (!issues.Any(issue => issue.Property == property.Name))
                        failures.Add($"{descriptor.OperationType.Name}.{property.Name}");
                }
            }

            Assert.AreEqual(0, failures.Count,
                "Infinity was accepted by: " + string.Join(", ", failures));
        }

        [TestMethod]
        public void ImportedPolyline_NonFiniteCoordinate_IsRejectedByPointPath()
        {
            var operation = OperationFixtures.ProfileDxf();
            operation.Polylines[0].Points[1].X = double.NaN;

            var issue = operation.Validate().Single(item => item.Code == ValidationCode.NotFinite);

            Assert.AreEqual("Polylines[0].Points[1].X", issue.Property);
        }

        [TestMethod]
        public void WorkCoordinate_NonFiniteEnabledValue_StopsGeneration()
        {
            var settings = SettingsFixtures.Default();
            settings.WorkCoordinate.AddEndPosition = true;
            settings.WorkCoordinate.EndX = double.PositiveInfinity;

            var error = Assert.Throws<GCodeGenerationValidationException>(() =>
                new SimpleGCodeGenerator().Generate(
                    new List<OperationBase> { OperationFixtures.ProfileCircle() }, settings));

            Assert.IsTrue(error.SettingsIssues.Any(issue => issue.Property == nameof(WorkCoordinateSettings.EndX)));
        }

        [TestMethod]
        public void LowLevelGCodeAndToolPath_RejectNonFiniteValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GCodeWord.X(double.NaN, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ToolMove(ToolMoveKind.Linear, x: double.PositiveInfinity, y: 0, z: 0, feed: 100));
        }

        [TestMethod]
        public void WallTaper_OutOfRange_IsReportedInsteadOfClamped()
        {
            var operation = OperationFixtures.PocketCircle();
            operation.WallTaperAngleDeg = 90;

            var issue = operation.Validate().Single(item => item.Property == nameof(operation.WallTaperAngleDeg));

            Assert.AreEqual(ValidationCode.AboveMaximum, issue.Code);
            Assert.AreEqual(90, operation.WallTaperAngleDeg, "Исходное ошибочное значение осталось видимым");
        }
    }
}
