using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drilling holes operation with individual hole Z-parameters
    /// and common XY feeds & safety settings.
    ///
    /// The pattern is described by <see cref="DrillMode"/> and the typed
    /// parameters below (plan item 3.1); <see cref="Holes"/> always holds the
    /// concrete hole list that the generator drills.
    /// </summary>
    public class DrillPointsOperation : OperationBase
    {
        public DrillPointsOperation() : base(OperationType.DrillPoints, "Drill points")
        {
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Drill pattern (plan item 3.1). Defaults to <see cref="DrillMode.Points"/>
        /// so that legacy files without this field (and manually created operations)
        /// keep the previous "individual holes" behavior.
        /// </summary>
        public DrillMode DrillMode { get; set; } = DrillMode.Points;

        /// <summary>
        /// Holes with full coordinates and Z parameters.
        /// Setter is needed for JSON deserialization of saved projects.
        /// </summary>
        public List<DrillHole> Holes { get; set; } = new List<DrillHole>();

        /// <summary>
        /// Rapid feed in XY plane (G0).
        /// </summary>
        public double FeedXYRapid { get; set; } = 1000.0;

        /// <summary>
        /// Working feed in XY plane (G1).
        /// </summary>
        public double FeedXYWork { get; set; } = 300.0;

        /// <summary>
        /// Safe Z height for moves between holes.
        /// </summary>
        public double SafeZBetweenHoles { get; set; } = 1.0;

        /// <summary>
        /// Number of decimal places for coordinates.
        /// </summary>
        public int Decimals { get; set; } = 3;

        // ------------------------------------------------------------------
        // Pattern parameters (plan item 3.1; previously stored in Metadata).
        // Defaults match the values the drill dialogs used to show for a new
        // operation of the corresponding mode.
        // ------------------------------------------------------------------

        // --- Line / Array / Rect pattern ---------------------------------

        /// <summary>Start point X of the line/grid pattern.</summary>
        public double StartX { get; set; }

        /// <summary>Start point Y of the line/grid pattern.</summary>
        public double StartY { get; set; }

        /// <summary>Start point Z of the line/grid pattern.</summary>
        public double StartZ { get; set; }

        /// <summary>Distance between neighboring holes in the pattern.</summary>
        public double Distance { get; set; } = 10.0;

        /// <summary>Number of holes per line (line mode) or per row (array/rect mode).</summary>
        public int HoleCount { get; set; } = 3;

        /// <summary>Pattern direction angle in degrees (0 = along X axis).</summary>
        public double AngleDeg { get; set; }

        /// <summary>Distance between rows (array/rect mode).</summary>
        public double RowPitch { get; set; } = 10.0;

        /// <summary>Number of rows (array/rect mode).</summary>
        public int RowCount { get; set; } = 2;

        // --- Circle / Arc / Polygon / Ellipse pattern ---------------------

        /// <summary>Center X of the circular pattern.</summary>
        public double CenterX { get; set; }

        /// <summary>Center Y of the circular pattern.</summary>
        public double CenterY { get; set; }

        /// <summary>Contour height (Z) of the circular pattern.</summary>
        public double Z { get; set; }

        /// <summary>Radius of the circle/arc/polygon pattern.</summary>
        public double Radius { get; set; } = 10.0;

        /// <summary>Start angle of the circle/arc/ellipse pattern in degrees.</summary>
        public double StartAngleDeg { get; set; }

        /// <summary>End angle of the arc pattern in degrees.</summary>
        public double EndAngleDeg { get; set; } = 90.0;

        /// <summary>Rotation angle of the polygon/ellipse pattern in degrees.</summary>
        public double RotationAngle { get; set; }

        /// <summary>Number of sides of the polygon pattern.</summary>
        public int NumberOfSides { get; set; } = 6;

        /// <summary>Number of holes per side of the polygon pattern.</summary>
        public int HolesPerSide { get; set; } = 2;

        /// <summary>Horizontal radius of the ellipse pattern.</summary>
        public double RadiusX { get; set; } = 10.0;

        /// <summary>Vertical radius of the ellipse pattern.</summary>
        public double RadiusY { get; set; } = 10.0;

        // --- Package pattern ----------------------------------------------

        /// <summary>
        /// Name of the package template (DIP8, SOIC-8, ...). Empty for a fresh
        /// operation: the dialog falls back to its default template (DIP8).
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        // --- Common Z parameters (applied to every generated hole) --------

        /// <summary>Total cutting depth for the pattern holes.</summary>
        public double TotalDepth { get; set; } = 2.0;

        /// <summary>Depth per pass for the pattern holes.</summary>
        public double StepDepth { get; set; } = 1.0;

        /// <summary>Rapid feed for Z for the pattern holes.</summary>
        public double FeedZRapid { get; set; } = 500.0;

        /// <summary>Working feed for Z for the pattern holes.</summary>
        public double FeedZWork { get; set; } = 200.0;

        /// <summary>Retract height for the pattern holes.</summary>
        public double RetractHeight { get; set; } = 0.3;

        /// <summary>
        /// Metadata for storing operation-specific parameters (e.g., line distance, array dimensions, circle radius, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

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
    }
}
