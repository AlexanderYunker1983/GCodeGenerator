#nullable enable
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for ellipse contour.
    /// </summary>
    public partial class ProfileEllipseOperation : ProfileOperationBase, IValidatable
    {
        public ProfileEllipseOperation() : base(OperationCategory.Profile, "Profile Ellipse")
        {
        }

        /// <summary>
        /// Ellipse center X coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerX = 0.0;

        /// <summary>
        /// Ellipse center Y coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerY = 0.0;

        /// <summary>
        /// Ellipse radius along X axis.
        /// </summary>
        [ObservableProperty]
        private double _radiusX = 10.0;

        /// <summary>
        /// Ellipse radius along Y axis.
        /// </summary>
        [ObservableProperty]
        private double _radiusY = 10.0;

        /// <summary>
        /// Rotation angle of the ellipse in degrees.
        /// </summary>
        [ObservableProperty]
        private double _rotationAngle = 0.0;

        public override string GetDescription()
        {
            return Invariant($"Ellipse RX{RadiusX}mm RY{RadiusY}mm at ({CenterX}, {CenterY}), depth {TotalDepth}mm");
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the ellipse radii.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddProfileIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
            return issues;
        }
    }
}

