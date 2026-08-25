using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for rounded rectangle contour.
    /// </summary>
    public class ProfileRoundedRectangleOperation : ProfileOperationBase, IValidatable
    {
        public ProfileRoundedRectangleOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Rounded Rectangle")
        {
        }

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
        /// Corner radius (top left).
        /// </summary>
        public double RadiusTopLeft { get; set; } = 2.0;

        /// <summary>
        /// Corner radius (top right).
        /// </summary>
        public double RadiusTopRight { get; set; } = 2.0;

        /// <summary>
        /// Corner radius (bottom left).
        /// </summary>
        public double RadiusBottomLeft { get; set; } = 2.0;

        /// <summary>
        /// Corner radius (bottom right).
        /// </summary>
        public double RadiusBottomRight { get; set; } = 2.0;

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

        public override string GetDescription()
        {
            return $"Rounded rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the rectangle dimensions. Corner radii are not validated: the
        /// geometry clamps them (negative → 0, oversized → half of the
        /// smaller side), so such values still generate a valid contour.
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


