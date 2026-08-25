using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for rectangle contour.
    /// </summary>
    public class ProfileRectangleOperation : MillingOperationBase, IProfileOperation, IValidatable
    {
        public ProfileRectangleOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Rectangle")
        {
        }

        /// <summary>
        /// Tool path mode: on line, outside, or inside contour.
        /// </summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>
        /// Rectangle width.
        /// </summary>
        public double Width { get; set; } = 10.0;

        /// <summary>
        /// Rectangle height.
        /// </summary>
        public double Height { get; set; } = 10.0;

        /// <summary>
        /// Rotation angle in degrees.
        /// </summary>
        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Reference point X coordinate.
        /// </summary>
        public double ReferencePointX { get; set; } = 0.0;

        /// <summary>
        /// Reference point Y coordinate.
        /// </summary>
        public double ReferencePointY { get; set; } = 0.0;

        /// <summary>
        /// Reference point type (center, corner, etc.).
        /// </summary>
        public ReferencePointType ReferencePointType { get; set; } = ReferencePointType.Center;

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
            return $"Rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the rectangle dimensions.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Width), Width);
            OperationValidation.AddIfNotPositive(issues, nameof(Height), Height);
            return issues;
        }
    }
}

