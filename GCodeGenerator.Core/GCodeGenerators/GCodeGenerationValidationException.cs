using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// An enabled operation cannot safely be converted to G-code.
    /// Contains all failures found by the preflight pass, before any program
    /// blocks are emitted.
    /// </summary>
    public sealed class GCodeGenerationValidationException : InvalidOperationException
    {
        public GCodeGenerationValidationException(IEnumerable<OperationValidationFailure> failures)
            : this(failures, null)
        {
        }

        /// <summary>
        /// Проблемы операций и настроек, найденные до построения программы.
        /// </summary>
        /// <param name="failures">Проблемы отдельных операций.</param>
        /// <param name="settingsIssues">Проблемы настроек генерации.</param>
        public GCodeGenerationValidationException(
            IEnumerable<OperationValidationFailure> failures,
            IEnumerable<ValidationIssue> settingsIssues)
            : this((failures ?? throw new ArgumentNullException(nameof(failures))).ToArray(),
                   (settingsIssues ?? Enumerable.Empty<ValidationIssue>()).ToArray())
        {
        }

        private GCodeGenerationValidationException(
            OperationValidationFailure[] failures,
            ValidationIssue[] settingsIssues)
            : base(BuildMessage(failures, settingsIssues))
        {
            if (failures.Length == 0 && settingsIssues.Length == 0)
                throw new ArgumentException("At least one validation failure is required.", nameof(failures));

            Failures = new ReadOnlyCollection<OperationValidationFailure>(failures);
            SettingsIssues = new ReadOnlyCollection<ValidationIssue>(settingsIssues);
        }

        public IReadOnlyList<OperationValidationFailure> Failures { get; }

        /// <summary>Проблемы настроек генерации: система координат, шпиндель.</summary>
        public IReadOnlyList<ValidationIssue> SettingsIssues { get; }

        private static string BuildMessage(
            IReadOnlyList<OperationValidationFailure> failures,
            IReadOnlyList<ValidationIssue> settingsIssues)
        {
            if (failures.Count == 0 && settingsIssues.Count == 0)
                return "G-code generation validation failed.";

            var lines = settingsIssues.Select(issue => $"Settings: {issue}")
                .Concat(failures.Select(failure => failure.ToString()));

            return "G-code generation validation failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>A set of validation issues associated with one operation slot.</summary>
    public sealed class OperationValidationFailure
    {
        public OperationValidationFailure(
            int operationIndex,
            string operationName,
            string operationType,
            IEnumerable<ValidationIssue> issues)
        {
            if (operationIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(operationIndex));

            OperationIndex = operationIndex;
            OperationName = Normalize(operationName, "<unnamed>");
            OperationType = Normalize(operationType, "<null>");
            Issues = new ReadOnlyCollection<ValidationIssue>(
                (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray());

            if (Issues.Count == 0)
                throw new ArgumentException("At least one validation issue is required.", nameof(issues));
        }

        public int OperationIndex { get; }
        public string OperationName { get; }
        public string OperationType { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }

        public override string ToString()
        {
            return $"Operation #{OperationIndex + 1} \"{OperationName}\" ({OperationType}): "
                + string.Join("; ", Issues.Select(issue => issue.ToString()));
        }

        private static string Normalize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
