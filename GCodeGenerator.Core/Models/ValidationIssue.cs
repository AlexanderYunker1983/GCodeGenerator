using System;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// A single problem found by domain validation (plan item 3.7).
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(string property, string message)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Name of the property that failed validation (e.g. "StepDepth",
        /// "Holes[2].TotalDepth", "ClosedContours[0]").
        /// </summary>
        public string Property { get; }

        /// <summary>
        /// Human-readable description of the problem.
        /// </summary>
        public string Message { get; }

        public override string ToString() => $"{Property}: {Message}";
    }
}
