using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.Geometry;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Shared primitives for the <see cref="IValidatable"/> implementations
    /// (plan item 3.7). Checks are conservative: only physically impossible
    /// values are reported, so projects the app can actually generate are
    /// never flagged.
    /// </summary>
    public static class OperationValidation
    {
        /// <summary>
        /// Tolerance for closed-contour checks. Matches the DXF importer's
        /// closedness tolerance (0.001) so contours imported by the app are
        /// never reported as open.
        /// </summary>
        public const double ContourClosedTolerance = GeometryTolerances.PointCoincidence;

        /// <summary>
        /// Adds an issue if <paramref name="value"/> is non-finite or not greater than zero.
        /// </summary>
        public static void AddIfNotPositive(IList<ValidationIssue> issues, string property, double value)
        {
            if (!double.IsFinite(value) || value <= 0)
                issues.Add(new ValidationIssue(property,
                    $"must be finite and greater than zero, but is {value.ToString(CultureInfo.InvariantCulture)}"));
        }

        /// <summary>
        /// Adds an issue if <paramref name="value"/> is below <paramref name="min"/>.
        /// </summary>
        public static void AddIfBelow(IList<ValidationIssue> issues, string property, int value, int min)
        {
            if (value < min)
                issues.Add(new ValidationIssue(property,
                    $"must be at least {min}, but is {value}"));
        }

        /// <summary>
        /// Adds issues for the common milling parameters: TotalDepth, StepDepth
        /// and ToolDiameter must all be greater than zero.
        /// </summary>
        public static void AddCommonMillingIssues(IList<ValidationIssue> issues, double totalDepth, double stepDepth, double toolDiameter)
        {
            AddIfNotPositive(issues, "TotalDepth", totalDepth);
            AddIfNotPositive(issues, "StepDepth", stepDepth);
            AddIfNotPositive(issues, "ToolDiameter", toolDiameter);
        }

        /// <summary>
        /// True if the first and last points of the contour coincide within
        /// <see cref="ContourClosedTolerance"/>.
        /// </summary>
        public static bool IsContourClosed(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 2)
                return false;

            var first = contour.Points[0];
            var last = contour.Points[contour.Points.Count - 1];
            if (first == null || last == null)
                return false;

            double dx = first.X - last.X;
            double dy = first.Y - last.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= ContourClosedTolerance;
        }
    }
}
