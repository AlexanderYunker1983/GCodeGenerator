using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for elliptical pocket.
    /// </summary>
    public class PocketEllipseOperation : OperationBase, IPocketOperation
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

        /// <summary>
        /// Угол линий для стратегии Lines (градусы к оси X).
        /// </summary>
        public double LineAngleDeg { get; set; } = 0.0;

        /// <summary>
        /// Уклон стенки, градусы (0 – вертикально). Положительные значения дают сужение внутрь к низу.
        /// </summary>
        public double WallTaperAngleDeg { get; set; } = 0.0;

        /// <summary>
        /// Включена ли черновая обработка (с припуском).
        /// </summary>
        public bool IsRoughingEnabled { get; set; }

        /// <summary>
        /// Включена ли чистовая обработка (с припуском).
        /// </summary>
        public bool IsFinishingEnabled { get; set; }

        /// <summary>
        /// Припуск на обработку (мм), используется по контуру и по глубине.
        /// </summary>
        public double FinishAllowance { get; set; } = 0.0;

        /// <summary>
        /// Режим чистовой обработки.
        /// </summary>
        public PocketFinishingMode FinishingMode { get; set; } = PocketFinishingMode.All;

        /// <summary>
        /// Включено ли фрезерование острова (обработка области вокруг острова).
        /// </summary>
        public bool IsIslandMillingEnabled { get; set; } = false;

        /// <summary>
        /// Тип внешней границы для фрезерования острова.
        /// </summary>
        public OuterBoundaryType OuterBoundaryType { get; set; } = OuterBoundaryType.Rectangle;

        /// <summary>
        /// Центр внешней границы по X.
        /// </summary>
        public double OuterBoundaryCenterX { get; set; } = 0.0;

        /// <summary>
        /// Центр внешней границы по Y.
        /// </summary>
        public double OuterBoundaryCenterY { get; set; } = 0.0;

        /// <summary>
        /// Ширина внешней границы (для прямоугольника) или диаметр по X (для эллипса).
        /// </summary>
        public double OuterBoundaryWidth { get; set; } = 50.0;

        /// <summary>
        /// Высота внешней границы (для прямоугольника) или диаметр по Y (для эллипса).
        /// </summary>
        public double OuterBoundaryHeight { get; set; } = 50.0;

        public Dictionary<string, object> Metadata { get; set; }

        public override string GetDescription()
        {
            return $"Pocket ellipse RX{RadiusX}mm RY{RadiusY}mm, depth {TotalDepth}mm";
        }
    }
}

