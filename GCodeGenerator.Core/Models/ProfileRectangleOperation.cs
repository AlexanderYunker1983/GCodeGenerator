using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for rectangle contour.
    /// </summary>
    public class ProfileRectangleOperation : ProfileOperationBase, IValidatable
    {
        public ProfileRectangleOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Rectangle")
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

