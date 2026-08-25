#nullable enable
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for rectangular pocket.
    /// </summary>
    public partial class PocketRectangleOperation : PocketOperationBase, IValidatable
    {
        public PocketRectangleOperation() : base(OperationCategory.Pocket, "Pocket Rectangle")
        {
        }
        [ObservableProperty]
        private double _width = 10.0;

        [ObservableProperty]

        private double _height = 10.0;

        [ObservableProperty]

        private double _rotationAngle = 0.0;

        [ObservableProperty]

        private double _referencePointX = 0.0;

        [ObservableProperty]

        private double _referencePointY = 0.0;

        [ObservableProperty]

        private ReferencePointType _referencePointType = ReferencePointType.Center;

        public override string GetDescription()
        {
            return Invariant($"Pocket rectangle {Width}x{Height}mm, depth {TotalDepth}mm");
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the pocket dimensions.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddPocketIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(Width), Width);
            OperationValidation.AddIfNotPositive(issues, nameof(Height), Height);
            return issues;
        }
    }
}


