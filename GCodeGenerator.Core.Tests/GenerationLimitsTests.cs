#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public sealed class GenerationLimitsTests
    {
        private sealed class SingleRegistry : IOperationGeneratorRegistry
        {
            private readonly IOperationGenerator _generator;

            public SingleRegistry(IOperationGenerator generator) => _generator = generator;

            public bool TryGetGenerator(Type operationType, out IOperationGenerator? generator)
            {
                generator = operationType == typeof(ProfileCircleOperation) ? _generator : null;
                return generator != null;
            }
        }

        private sealed class OversizedToolPathGenerator : IOperationGenerator
        {
            public void Generate(
                OperationBase operation,
                ToolPathBuilder builder,
                GCodeSettings settings,
                CancellationToken cancellation = default)
            {
                for (var index = 0; index <= GenerationLimits.MaxToolPathItems; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    builder.RapidTo(x: index);
                }
            }
        }

        [TestMethod]
        public void HugeDrillArray_IsRejectedWithoutMaterializingIt()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Array);
            operation.HoleCount = int.MaxValue;
            operation.RowCount = int.MaxValue;

            var issue = operation.Validate().Single(item => item.Code == ValidationCode.AboveMaximum);

            Assert.AreEqual(nameof(operation.HoleCount), issue.Property);
            Assert.AreEqual(0, operation.HolesToDrill.Count,
                "Неверная расстановка не выделяет память под отверстия");
        }

        [TestMethod]
        public void ArcWithInfiniteAngles_DoesNotLoopBeforeValidation()
        {
            var operation = DrillPointsOperation.CreateNew(DrillMode.Arc);
            operation.StartAngleDeg = double.PositiveInfinity;

            Assert.AreEqual(0, operation.HolesToDrill.Count,
                "Описание операции безопасно и до предполётной проверки");
            Assert.IsTrue(operation.Validate().Any(item => item.Property == nameof(operation.StartAngleDeg)));
        }

        [TestMethod]
        public void OversizedDxfGeometry_IsRejectedByAggregateCount()
        {
            var operation = OperationFixtures.ProfileDxf();
            operation.Polylines = Enumerable.Range(
                    0,
                    GenerationLimits.MaxImportedContoursPerOperation + 1)
                .Select(_ => new Polyline2D())
                .ToList();

            var issue = operation.Validate().Single(item => item.Property == nameof(operation.Polylines));

            Assert.AreEqual(ValidationCode.AboveMaximum, issue.Code);
        }

        [TestMethod]
        public void OversizedProject_IsRejectedBeforeOperationValidation()
        {
            var operation = OperationFixtures.ProfileCircle();
            var operations = Enumerable.Repeat<OperationBase?>(
                operation,
                GenerationLimits.MaxOperations + 1).ToList();

            var error = Assert.Throws<GCodeGenerationValidationException>(() =>
                new SimpleGCodeGenerator().BuildToolPath(operations, new GCodeSettings()));

            Assert.AreEqual("Operations", error.SettingsIssues.Single().Property);
        }

        [TestMethod]
        public void ToolPathBudget_StopsUnexpectedGeometryExplosion()
        {
            var generator = new SimpleGCodeGenerator(
                new SingleRegistry(new OversizedToolPathGenerator()));

            var error = Assert.Throws<GCodeGenerationValidationException>(() =>
                generator.BuildToolPath(
                    new List<OperationBase?> { OperationFixtures.ProfileCircle() },
                    new GCodeSettings()));

            var issue = error.Failures.Single().Issues.Single();
            Assert.AreEqual("ToolPath", issue.Property);
            Assert.AreEqual(ValidationCode.AboveMaximum, issue.Code);
        }

        [TestMethod]
        public void PostProcessorAndFormatter_HonorCancellation()
        {
            var path = new ToolPath();
            var operation = new ToolPathOperation("test", "test", 3);
            path.AddOperation(operation);
            new ToolPathBuilder(operation).RapidTo(x: 1);

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(() =>
                new GenericPostProcessor().Build(path, new GCodeSettings(), canceled.Token));
            Assert.ThrowsExactly<OperationCanceledException>(() =>
                GCodeFormatter.Format(new GCodeProgram(), new GCodeSettings(), canceled.Token));
            Assert.ThrowsExactly<OperationCanceledException>(() =>
                DrillPointsOperation.CreateNew(DrillMode.Array).GetHolesToDrill(canceled.Token));
        }
    }
}
