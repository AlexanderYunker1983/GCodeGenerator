using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for circle contour.
    /// </summary>
    public class ProfileCircleOperation : ProfileOperationBase, IValidatable
    {
        public ProfileCircleOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Circle")
        {
        }

        /// <summary>
        /// Circle center X coordinate.
        /// </summary>
        public double CenterX { get; set; } = 0.0;

        /// <summary>
        /// Circle center Y coordinate.
        /// </summary>
        public double CenterY { get; set; } = 0.0;

        /// <summary>
        /// Circle radius.
        /// </summary>
        public double Radius { get; set; } = 10.0;

        public override string GetDescription()
        {
            return $"Circle R{Radius}mm at ({CenterX}, {CenterY}), depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the contour radius.
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

