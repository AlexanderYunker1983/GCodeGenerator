using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for ellipse contour.
    /// </summary>
    public class ProfileEllipseOperation : MillingOperationBase, IProfileOperation, IValidatable
    {
        public ProfileEllipseOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Ellipse")
        {
        }

        /// <summary>
        /// Tool path mode: on line, outside, or inside contour.
        /// </summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>
        /// Ellipse center X coordinate.
        /// </summary>
        public double CenterX { get; set; } = 0.0;

        /// <summary>
        /// Ellipse center Y coordinate.
        /// </summary>
        public double CenterY { get; set; } = 0.0;

        /// <summary>
        /// Ellipse radius along X axis.
        /// </summary>
        public double RadiusX { get; set; } = 10.0;

        /// <summary>
        /// Ellipse radius along Y axis.
        /// </summary>
        public double RadiusY { get; set; } = 10.0;

        /// <summary>
        /// Rotation angle of the ellipse in degrees.
        /// </summary>
        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Maximum segment length for arc approximation when arc support is disabled.
        /// </summary>
        public double MaxSegmentLength { get; set; } = 0.5;

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

        public override string GetDescription()
        {
            return $"Ellipse RX{RadiusX}mm RY{RadiusY}mm at ({CenterX}, {CenterY}), depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the ellipse radii.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
            return issues;
        }
    }
}

