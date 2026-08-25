using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drilling holes operation with individual hole Z-parameters
    /// and common XY feeds and safety settings.
    ///
    /// The pattern is described by <see cref="DrillMode"/> and the typed
    /// parameters below (plan item 3.1); <see cref="Holes"/> always holds the
    /// concrete hole list that the generator drills.
    /// </summary>
    public partial class DrillPointsOperation : OperationBase, IValidatable
    {
        public DrillPointsOperation() : base(OperationType.DrillPoints, OperationCategory.Drill, "Drill points")
        {
        }

        /// <summary>
        /// Drill pattern (plan item 3.1). Defaults to <see cref="DrillMode.Points"/>
        /// so that legacy files without this field (and manually created operations)
        /// keep the previous "individual holes" behavior.
        /// </summary>
        [ObservableProperty]
        private DrillMode _drillMode = DrillMode.Points;

        /// <summary>
        /// Holes with full coordinates and Z parameters.
        /// Setter is needed for JSON deserialization of saved projects.
        /// </summary>
        [ObservableProperty]
        private List<DrillHole> _holes = new List<DrillHole>();

        /// <summary>
        /// Rapid feed in XY plane (G0).
        /// </summary>
        [ObservableProperty]
        private double _feedXYRapid = 1000.0;

        /// <summary>
        /// Working feed in XY plane (G1).
        /// </summary>
        [ObservableProperty]
        private double _feedXYWork = 300.0;

        /// <summary>
        /// Safe Z height for moves between holes.
        /// </summary>
        [ObservableProperty]
        private double _safeZBetweenHoles = 1.0;

        /// <summary>
        /// Number of decimal places for coordinates.
        /// </summary>
        [ObservableProperty]
        private int _decimals = 3;

        // ------------------------------------------------------------------
        // Pattern parameters (plan item 3.1; previously stored in Metadata).
        // Defaults match the values the drill dialogs used to show for a new
        // operation of the corresponding mode.
        // ------------------------------------------------------------------

        // --- Line / Array / Rect pattern ---------------------------------

        /// <summary>Start point X of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startX;

        /// <summary>Start point Y of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startY;

        /// <summary>Start point Z of the line/grid pattern.</summary>
        [ObservableProperty]
        private double _startZ;

        /// <summary>Distance between neighboring holes in the pattern.</summary>
        [ObservableProperty]
        private double _distance = 10.0;

        /// <summary>Number of holes per line (line mode) or per row (array/rect mode).</summary>
        [ObservableProperty]
        private int _holeCount = 3;

        /// <summary>Pattern direction angle in degrees (0 = along X axis).</summary>
        [ObservableProperty]
        private double _angleDeg;

        /// <summary>Distance between rows (array/rect mode).</summary>
        [ObservableProperty]
        private double _rowPitch = 10.0;

        /// <summary>Number of rows (array/rect mode).</summary>
        [ObservableProperty]
        private int _rowCount = 2;

        // --- Circle / Arc / Polygon / Ellipse pattern ---------------------

        /// <summary>Center X of the circular pattern.</summary>
        [ObservableProperty]
        private double _centerX;

        /// <summary>Center Y of the circular pattern.</summary>
        [ObservableProperty]
        private double _centerY;

        /// <summary>Contour height (Z) of the circular pattern.</summary>
        [ObservableProperty]
        private double _z;

        /// <summary>Radius of the circle/arc/polygon pattern.</summary>
        [ObservableProperty]
        private double _radius = 10.0;

        /// <summary>Start angle of the circle/arc/ellipse pattern in degrees.</summary>
        [ObservableProperty]
        private double _startAngleDeg;

        /// <summary>End angle of the arc pattern in degrees.</summary>
        [ObservableProperty]
        private double _endAngleDeg = 90.0;

        /// <summary>Rotation angle of the polygon/ellipse pattern in degrees.</summary>
        [ObservableProperty]
        private double _rotationAngle;

        /// <summary>Number of sides of the polygon pattern.</summary>
        [ObservableProperty]
        private int _numberOfSides = 6;

        /// <summary>Number of holes per side of the polygon pattern.</summary>
        [ObservableProperty]
        private int _holesPerSide = 2;

        /// <summary>Horizontal radius of the ellipse pattern.</summary>
        [ObservableProperty]
        private double _radiusX = 10.0;

        /// <summary>Vertical radius of the ellipse pattern.</summary>
        [ObservableProperty]
        private double _radiusY = 10.0;

        // --- Package pattern ----------------------------------------------

        /// <summary>
        /// Name of the package template (DIP8, SOIC-8, ...). Empty for a fresh
        /// operation: the dialog falls back to its default template (DIP8).
        /// </summary>
        [ObservableProperty]
        private string _packageName = string.Empty;

        // --- Common Z parameters (applied to every generated hole) --------

        /// <summary>Total cutting depth for the pattern holes.</summary>
        [ObservableProperty]
        private double _totalDepth = 2.0;

        /// <summary>Depth per pass for the pattern holes.</summary>
        [ObservableProperty]
        private double _stepDepth = 1.0;

        /// <summary>Rapid feed for Z for the pattern holes.</summary>
        [ObservableProperty]
        private double _feedZRapid = 500.0;

        /// <summary>Working feed for Z for the pattern holes.</summary>
        [ObservableProperty]
        private double _feedZWork = 200.0;

        /// <summary>Retract height for the pattern holes.</summary>
        [ObservableProperty]
        private double _retractHeight = 0.3;

        /// <summary>
        /// Creates a fresh operation for the given drill mode with the default
        /// parameters the dialog used to show for a new operation of that mode
        /// (plan item 3.1).
        /// </summary>
        public static DrillPointsOperation CreateNew(DrillMode mode)
        {
            var operation = new DrillPointsOperation { DrillMode = mode };
            // Circular patterns (circle/arc/ellipse) used to default to 2 holes
            // in the dialog, which differs from the model default (3).
            if (mode == DrillMode.Circle || mode == DrillMode.Arc || mode == DrillMode.Ellipse)
                operation.HoleCount = 2;
            return operation;
        }

        public override string GetDescription()
        {
            return $"Drill {Holes.Count} hole(s)";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): the drilled hole list, the
        /// per-hole Z parameters and the mode-specific pattern parameters.
        /// Point values that the generators can handle are never flagged.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();

            // The generator drills exactly this list in every mode.
            if (Holes == null || Holes.Count == 0)
            {
                issues.Add(new ValidationIssue(nameof(Holes), "no holes to drill"));
            }
            else
            {
                for (int i = 0; i < Holes.Count; i++)
                {
                    var hole = Holes[i];
                    if (hole == null)
                    {
                        issues.Add(new ValidationIssue($"Holes[{i}]", "hole is null"));
                        continue;
                    }
                    OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].TotalDepth", hole.TotalDepth);
                    OperationValidation.AddIfNotPositive(issues, $"Holes[{i}].StepDepth", hole.StepDepth);
                }
            }

            // Pattern modes share common Z parameters; Points mode keeps
            // per-hole Z parameters in Holes only.
            if (DrillMode != DrillMode.Points)
            {
                OperationValidation.AddIfNotPositive(issues, nameof(TotalDepth), TotalDepth);
                OperationValidation.AddIfNotPositive(issues, nameof(StepDepth), StepDepth);
            }

            switch (DrillMode)
            {
                case DrillMode.Line:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Distance), Distance);
                    break;
                case DrillMode.Array:
                case DrillMode.Rect:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Distance), Distance);
                    OperationValidation.AddIfBelow(issues, nameof(RowCount), RowCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(RowPitch), RowPitch);
                    break;
                case DrillMode.Circle:
                case DrillMode.Arc:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
                    break;
                case DrillMode.Polygon:
                    OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
                    OperationValidation.AddIfBelow(issues, nameof(NumberOfSides), NumberOfSides, 3);
                    OperationValidation.AddIfBelow(issues, nameof(HolesPerSide), HolesPerSide, 1);
                    break;
                case DrillMode.Ellipse:
                    OperationValidation.AddIfBelow(issues, nameof(HoleCount), HoleCount, 1);
                    OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
                    OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
                    break;
                case DrillMode.Package:
                    // PackageName may be empty: the dialog falls back to its default template.
                    break;
                case DrillMode.Points:
                default:
                    break;
            }

            return issues;
        }
    }
}
