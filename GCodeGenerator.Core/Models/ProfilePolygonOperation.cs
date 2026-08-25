using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for regular polygon contour.
    /// </summary>
    public partial class ProfilePolygonOperation : ProfileOperationBase, IValidatable
    {
        public ProfilePolygonOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile Polygon")
        {
        }

        /// <summary>
        /// Polygon center X coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerX = 0.0;

        /// <summary>
        /// Polygon center Y coordinate.
        /// </summary>
        [ObservableProperty]
        private double _centerY = 0.0;

        /// <summary>
        /// Number of sides (minimum 3).
        /// </summary>
        [ObservableProperty]
        private int _numberOfSides = 6;

        /// <summary>
        /// Radius of the circumscribed circle.
        /// </summary>
        [ObservableProperty]
        private double _radius = 10.0;

        /// <summary>
        /// Rotation angle of the polygon in degrees.
        /// </summary>
        [ObservableProperty]
        private double _rotationAngle = 0.0;

        public override string GetDescription()
        {
            return $"Polygon {NumberOfSides}-sided R{Radius}mm at ({CenterX}, {CenterY}), depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters, the
        /// contour radius and the side count (a polygon needs at least 3 sides).
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            OperationValidation.AddIfBelow(issues, nameof(NumberOfSides), NumberOfSides, 3);
            return issues;
        }
    }
}

