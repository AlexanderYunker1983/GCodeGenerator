using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    public class DxfPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class DxfPolyline
    {
        public List<DxfPoint> Points { get; set; } = new List<DxfPoint>();
    }

    /// <summary>
    /// Profile milling operation imported from DXF lines.
    /// </summary>
    public class ProfileDxfOperation : OperationBase
    {
        public ProfileDxfOperation() : base(OperationType.ProfileMilling, "Profile DXF")
        {
        }

        public List<DxfPolyline> Polylines { get; set; } = new List<DxfPolyline>();

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

        public int Decimals { get; set; } = 3;

        public override string GetDescription()
        {
            var lines = 0;
            foreach (var poly in Polylines)
                lines += poly?.Points?.Count > 1 ? poly.Points.Count - 1 : 0;
            return $"DXF profile lines: {lines}";
        }
    }
}


