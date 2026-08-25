using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for rounded rectangle contour.
    /// </summary>
    public partial class ProfileRoundedRectangleOperation : ProfileOperationBase, IValidatable
    {
        public ProfileRoundedRectangleOperation() : base(OperationCategory.Profile, "Profile Rounded Rectangle")
        {
        }

        /// <summary>
        /// Rectangle width.
        /// </summary>
        [ObservableProperty]
        private double _width = 10.0;

        /// <summary>
        /// Rectangle height.
        /// </summary>
        [ObservableProperty]
        private double _height = 10.0;

        /// <summary>
        /// Rotation angle in degrees.
        /// </summary>
        [ObservableProperty]
        private double _rotationAngle = 0.0;

        /// <summary>
        /// Corner radius (top left).
        /// </summary>
        [ObservableProperty]
        private double _radiusTopLeft = 2.0;

        /// <summary>
        /// Corner radius (top right).
        /// </summary>
        [ObservableProperty]
        private double _radiusTopRight = 2.0;

        /// <summary>
        /// Corner radius (bottom left).
        /// </summary>
        [ObservableProperty]
        private double _radiusBottomLeft = 2.0;

        /// <summary>
        /// Corner radius (bottom right).
        /// </summary>
        [ObservableProperty]
        private double _radiusBottomRight = 2.0;

        /// <summary>
        /// Reference point X coordinate.
        /// </summary>
        [ObservableProperty]
        private double _referencePointX = 0.0;

        /// <summary>
        /// Reference point Y coordinate.
        /// </summary>
        [ObservableProperty]
        private double _referencePointY = 0.0;

        /// <summary>
        /// Reference point type (center, corner, etc.).
        /// </summary>
        [ObservableProperty]
        private ReferencePointType _referencePointType = ReferencePointType.Center;

        public override string GetDescription()
        {
            return $"Rounded rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the rectangle dimensions. Corner radii are not validated: the
        /// geometry clamps them (negative → 0, oversized → half of the
        /// smaller side), so such values still generate a valid contour.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddProfileIssues(issues, this);
            OperationValidation.AddIfNotPositive(issues, nameof(Width), Width);
            OperationValidation.AddIfNotPositive(issues, nameof(Height), Height);
            return issues;
        }
    }
}


