using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation for rectangle contour.
    /// </summary>
    public class ProfileRectangleOperation : OperationBase, IProfileOperation
    {
        public ProfileRectangleOperation() : base(OperationType.ProfileMilling, "Profile Rectangle")
        {
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Tool path mode: on line, outside, or inside contour.
        /// </summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>
        /// Milling direction: clockwise or counter-clockwise.
        /// </summary>
        public MillingDirection Direction { get; set; } = MillingDirection.Clockwise;

        /// <summary>
        /// Rectangle width.
        /// </summary>
        public double Width { get; set; } = 10.0;

        /// <summary>
        /// Rectangle height.
        /// </summary>
        public double Height { get; set; } = 10.0;

        /// <summary>
        /// Rotation angle in degrees.
        /// </summary>
        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Total cutting depth.
        /// </summary>
        public double TotalDepth { get; set; } = 2.0;

        /// <summary>
        /// Depth per pass.
        /// </summary>
        public double StepDepth { get; set; } = 1.0;

        /// <summary>
        /// Tool diameter.
        /// </summary>
        public double ToolDiameter { get; set; } = 3.0;

        /// <summary>
        /// Contour height (Z coordinate).
        /// </summary>
        public double ContourHeight { get; set; } = 0.0;

        /// <summary>
        /// Rapid feed in XY plane (G0).
        /// </summary>
        public double FeedXYRapid { get; set; } = 1000.0;

        /// <summary>
        /// Working feed in XY plane (G1).
        /// </summary>
        public double FeedXYWork { get; set; } = 300.0;

        /// <summary>
        /// Rapid feed for Z (G0).
        /// </summary>
        public double FeedZRapid { get; set; } = 500.0;

        /// <summary>
        /// Working feed for Z (G1).
        /// </summary>
        public double FeedZWork { get; set; } = 200.0;

        /// <summary>
        /// Safe Z height for moves.
        /// </summary>
        public double SafeZHeight { get; set; } = 1.0;

        /// <summary>
        /// Retract height.
        /// </summary>
        public double RetractHeight { get; set; } = 0.3;

        /// <summary>
        /// Reference point X coordinate.
        /// </summary>
        public double ReferencePointX { get; set; } = 0.0;

        /// <summary>
        /// Reference point Y coordinate.
        /// </summary>
        public double ReferencePointY { get; set; } = 0.0;

        /// <summary>
        /// Reference point type (center, corner, etc.).
        /// </summary>
        public ReferencePointType ReferencePointType { get; set; } = ReferencePointType.Center;

        /// <summary>
        /// Tool entry mode: vertical or angled.
        /// </summary>
        public EntryMode EntryMode { get; set; } = EntryMode.Vertical;

        /// <summary>
        /// Entry angle in degrees (for angled entry).
        /// </summary>
        public double EntryAngle { get; set; } = 5.0;

        /// <summary>
        /// Safe distance between passes (for angled entry).
        /// </summary>
        public double SafeDistanceBetweenPasses { get; set; } = 1.0;

        /// <summary>
        /// Number of decimal places for coordinates.
        /// </summary>
        public int Decimals { get; set; } = 3;

        /// <summary>
        /// Maximum segment length for arc approximation when arc support is disabled.
        /// </summary>
        public double MaxSegmentLength { get; set; } = 0.5;

        /// <summary>
        /// Metadata for storing additional parameters.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        public override string GetDescription()
        {
            return $"Rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }
    }
}

