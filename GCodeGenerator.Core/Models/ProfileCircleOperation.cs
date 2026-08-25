using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for circle contour.
    /// </summary>
    public partial class ProfileCircleOperation : ProfileOperationBase, IValidatable
    {
        public ProfileCircleOperation() : base(OperationCategory.Profile, "Profile Circle")
        {
        }

        /// <summary>
        /// Circle center X coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerX = 0.0;

        /// <summary>
        /// Circle center Y coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerY = 0.0;

        /// <summary>
        /// Circle radius.
        /// </summary>
        [ObservableProperty]
        private double _radius = 10.0;

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
            OperationValidation.AddProfileIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            return issues;
        }
    }
}

