using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for regular polygon contour.
    /// </summary>
    public class ProfilePolygonOperation : MillingOperationBase, IProfileOperation, IValidatable
    {
        public ProfilePolygonOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Polygon")
        {
        }

        /// <summary>
        /// Tool path mode: on line, outside, or inside contour.
        /// </summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>
        /// Polygon center X coordinate.
        /// </summary>
        public double CenterX { get; set; } = 0.0;

        /// <summary>
        /// Polygon center Y coordinate.
        /// </summary>
        public double CenterY { get; set; } = 0.0;

        /// <summary>
        /// Number of sides (minimum 3).
        /// </summary>
        public int NumberOfSides { get; set; } = 6;

        /// <summary>
        /// Radius of the circumscribed circle.
        /// </summary>
        public double Radius { get; set; } = 10.0;

        /// <summary>
        /// Rotation angle of the polygon in degrees.
        /// </summary>
        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Tool entry mode: vertical or angled.
        /// </summary>
        public EntryMode EntryMode { get; set; } = EntryMode.Vertical;

        /// <summary>
        /// Entry angle in degrees (for angled entry).
        /// </summary>
        public double EntryAngle { get; set; } = 5.0;

        /// <summary>
        /// Safe distance between passes (for angled entry).
        /// </summary>
        public double SafeDistanceBetweenPasses { get; set; } = 1.0;

        /// <summary>
        /// Maximum segment length for arc approximation when arc support is disabled.
        /// </summary>
        public double MaxSegmentLength { get; set; } = 0.5;

        public override string GetDescription()
        {
            return $"Polygon {NumberOfSides}-sided R{Radius}mm at ({CenterX}, {CenterY}), depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters, the
        /// contour radius and the side count (a polygon needs at least 3 sides).
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            OperationValidation.AddIfBelow(issues, nameof(NumberOfSides), NumberOfSides, 3);
            return issues;
        }
    }
}

