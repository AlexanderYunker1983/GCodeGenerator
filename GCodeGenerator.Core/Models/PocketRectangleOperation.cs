using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for rectangular pocket.
    /// </summary>
    public class PocketRectangleOperation : PocketOperationBase, IValidatable
    {
        public PocketRectangleOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Rectangle")
        {
        }
        public double Width { get; set; } = 10.0;

        public double Height { get; set; } = 10.0;

        public double RotationAngle { get; set; } = 0.0;

        public double ReferencePointX { get; set; } = 0.0;

        public double ReferencePointY { get; set; } = 0.0;

        public ReferencePointType ReferencePointType { get; set; } = ReferencePointType.Center;

        public override string GetDescription()
        {
            return $"Pocket rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the pocket dimensions.
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


