using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for elliptical pocket.
    /// </summary>
    public class PocketEllipseOperation : PocketOperationBase, IValidatable
    {
        public PocketEllipseOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Ellipse")
        {
        }

        public double CenterX { get; set; } = 0.0;

        public double CenterY { get; set; } = 0.0;

        public double RadiusX { get; set; } = 10.0;

        public double RadiusY { get; set; } = 10.0;

        public double RotationAngle { get; set; } = 0.0;

        public override string GetDescription()
        {
            return $"Pocket ellipse RX{RadiusX}mm RY{RadiusY}mm, depth {TotalDepth}mm";
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

