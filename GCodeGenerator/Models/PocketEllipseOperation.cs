using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for elliptical pocket.
    /// </summary>
    public class PocketEllipseOperation : OperationBase
    {
        public PocketEllipseOperation() : base(OperationType.ProfileMilling, "Pocket Ellipse")
        {
            Metadata = new Dictionary<string, object>();
        }

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;

        public MillingDirection Direction { get; set; } = MillingDirection.Clockwise;

        public double CenterX { get; set; } = 0.0;

        public double CenterY { get; set; } = 0.0;

        public double RadiusX { get; set; } = 10.0;

        public double RadiusY { get; set; } = 10.0;

        public double RotationAngle { get; set; } = 0.0;

        public double TotalDepth { get; set; } = 2.0;

        public double StepDepth { get; set; } = 1.0;

        public double ToolDiameter { get; set; } = 3.0;

        public double ContourHeight { get; set; } = 0.0;

        public double FeedXYRapid { get; set; } = 1000.0;

        public double FeedXYWork { get; set; } = 300.0;

        public double FeedZRapid { get; set; } = 500.0;

        public double FeedZWork { get; set; } = 200.0;

        public double SafeZHeight { get; set; } = 1.0;

        public double RetractHeight { get; set; } = 0.3;

        /// <summary>
        /// Pocketing step as percent of tool diameter.
        /// </summary>
        public double StepPercentOfTool { get; set; } = 40.0;

        public int Decimals { get; set; } = 3;

        public Dictionary<string, object> Metadata { get; set; }

        public override string GetDescription()
        {
            return $"Pocket ellipse RX{RadiusX}mm RY{RadiusY}mm, depth {TotalDepth}mm";
        }
    }
}

