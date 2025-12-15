using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for rectangular pocket.
    /// </summary>
    public class PocketRectangleOperation : OperationBase
    {
        public PocketRectangleOperation() : base(OperationType.ProfileMilling, "Pocket Rectangle")
        {
            Metadata = new Dictionary<string, object>();
        }

        public MillingDirection Direction { get; set; } = MillingDirection.Clockwise;

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;
        public double Width { get; set; } = 10.0;

        public double Height { get; set; } = 10.0;

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

        public double ReferencePointX { get; set; } = 0.0;

        public double ReferencePointY { get; set; } = 0.0;

        public ReferencePointType ReferencePointType { get; set; } = ReferencePointType.Center;

        /// <summary>
        /// Pocketing step as percent of tool diameter (e.g., 40 => 40% of diameter).
        /// </summary>
        public double StepPercentOfTool { get; set; } = 40.0;

        public int Decimals { get; set; } = 3;

        /// <summary>
        /// Угол линий для стратегии Lines (градусы к оси X).
        /// </summary>
        public double LineAngleDeg { get; set; } = 0.0;

        /// <summary>
        /// Уклон стенки, градусы (0 – вертикально). Положительные значения дают сужение внутрь к низу.
        /// </summary>
        public double WallTaperAngleDeg { get; set; } = 0.0;

        public Dictionary<string, object> Metadata { get; set; }

        public override string GetDescription()
        {
            return $"Pocket rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }
    }
}


