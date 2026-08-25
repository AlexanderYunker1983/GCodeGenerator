using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class GenerationValidationTests
    {
        private sealed class RecordingOperationGenerator : IOperationGenerator
        {
            public int Calls { get; private set; }

            public void Generate(OperationBase operation, ToolPathBuilder builder, GCodeSettings settings)
            {
                Calls++;
            }
        }

        private sealed class SingleGeneratorRegistry : IOperationGeneratorRegistry
        {
            private readonly Type _operationType;
            private readonly IOperationGenerator _generator;

            public SingleGeneratorRegistry(Type operationType, IOperationGenerator generator)
            {
                _operationType = operationType;
                _generator = generator;
            }

            public bool TryGetGenerator(Type operationType, out IOperationGenerator generator)
            {
                generator = operationType == _operationType ? _generator : null;
                return generator != null;
            }
        }

        private static DrillPointsOperation ValidDrill(string name = "Valid drill")
        {
            return new DrillPointsOperation
            {
                Name = name,
                Holes = { new DrillHole { X = 1, Y = 2, TotalDepth = 2, StepDepth = 1 } },
            };
        }

        [TestMethod]
        public void InvalidOperation_StopsPreflightBeforeAnyGeneratorRuns()
        {
            var operationGenerator = new RecordingOperationGenerator();
            var generator = new SimpleGCodeGenerator(
                new SingleGeneratorRegistry(typeof(DrillPointsOperation), operationGenerator));
            var invalid = ValidDrill("Invalid drill");
            invalid.Holes[0].StepDepth = 0;

            var exception = Assert.Throws<GCodeGenerationValidationException>(() =>
                generator.Generate(
                    new List<OperationBase> { ValidDrill(), invalid },
                    new GCodeSettings()));

            Assert.AreEqual(0, operationGenerator.Calls, "Preflight must finish before emitting any operation blocks.");
            Assert.AreEqual(1, exception.Failures.Count);
            Assert.AreEqual(1, exception.Failures[0].OperationIndex);
            Assert.AreEqual("Holes[0].StepDepth", exception.Failures[0].Issues[0].Property);
        }

        [TestMethod]
        public void UnsupportedEnabledOperation_IsReportedInsteadOfSilentlySkipped()
        {
            var generator = new SimpleGCodeGenerator(
                new SingleGeneratorRegistry(typeof(ProfileCircleOperation), new RecordingOperationGenerator()));

            var exception = Assert.Throws<GCodeGenerationValidationException>(() =>
                generator.Generate(
                    new List<OperationBase> { ValidDrill("Missing generator") },
                    new GCodeSettings()));

            Assert.AreEqual("OperationType", exception.Failures[0].Issues[0].Property);
            StringAssert.Contains(exception.Message, "no G-code generator is registered");
        }

        [TestMethod]
        public void NonFinitePositiveParameters_AreRejected()
        {
            var operation = new ProfileCircleOperation
            {
                ToolDiameter = double.NaN,
                TotalDepth = double.PositiveInfinity,
            };

            var issues = operation.Validate();

            Assert.IsTrue(ContainsIssue(issues, nameof(ProfileCircleOperation.ToolDiameter)));
            Assert.IsTrue(ContainsIssue(issues, nameof(ProfileCircleOperation.TotalDepth)));
        }

        [TestMethod]
        public void NullDrillHole_IsRejected()
        {
            var operation = new DrillPointsOperation();
            operation.Holes.Add(null);

            var issues = operation.Validate();

            Assert.IsTrue(ContainsIssue(issues, "Holes[0]"));
        }

        private static bool ContainsIssue(IReadOnlyList<ValidationIssue> issues, string property)
        {
            foreach (var issue in issues)
            {
                if (issue.Property == property)
                    return true;
            }
            return false;
        }
    }
}
