using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for circular pocket.
    /// </summary>
    public partial class PocketCircleOperation : PocketOperationBase, IValidatable
    {
        public PocketCircleOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Circle")
        {
        }

        [ObservableProperty]

        private double _centerX = 0.0;

        [ObservableProperty]

        private double _centerY = 0.0;

        [ObservableProperty]

        private double _radius = 10.0;

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
            OperationValidation.AddPocketIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            return issues;
        }
    }
}


