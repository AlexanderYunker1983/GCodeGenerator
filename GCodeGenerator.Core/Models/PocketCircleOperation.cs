using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for circular pocket.
    /// </summary>
    public class PocketCircleOperation : PocketOperationBase, IValidatable
    {
        public PocketCircleOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Circle")
        {
        }

        public double CenterX { get; set; } = 0.0;

        public double CenterY { get; set; } = 0.0;

        public double Radius { get; set; } = 10.0;

        public override string GetDescription()
        {
            return $"Pocket circle R{Radius}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the pocket radius.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            return issues;
        }
    }
}


