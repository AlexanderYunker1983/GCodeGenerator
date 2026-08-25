#nullable enable
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for elliptical pocket.
    /// </summary>
    public partial class PocketEllipseOperation : PocketOperationBase, IValidatable
    {
        public PocketEllipseOperation() : base(OperationCategory.Pocket, "Pocket Ellipse")
        {
        }

        [ObservableProperty]

        private double _centerX = 0.0;

        [ObservableProperty]

        private double _centerY = 0.0;

        [ObservableProperty]

        private double _radiusX = 10.0;

        [ObservableProperty]

        private double _radiusY = 10.0;

        [ObservableProperty]

        private double _rotationAngle = 0.0;

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
            OperationValidation.AddPocketIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
            return issues;
        }
    }
}

