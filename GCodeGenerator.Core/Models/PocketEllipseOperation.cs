using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for elliptical pocket.
    /// </summary>
    public class PocketEllipseOperation : MillingOperationBase, IPocketOperation, IValidatable
    {
        public PocketEllipseOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Ellipse")
        {
        }

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;

        public double CenterX { get; set; } = 0.0;

        public double CenterY { get; set; } = 0.0;

        public double RadiusX { get; set; } = 10.0;

        public double RadiusY { get; set; } = 10.0;

        public double RotationAngle { get; set; } = 0.0;

        /// <summary>
        /// Pocketing step as percent of tool diameter.
        /// </summary>
        public double StepPercentOfTool { get; set; } = 40.0;

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

        public override string GetDescription()
        {
            return $"Pocket ellipse RX{RadiusX}mm RY{RadiusY}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the ellipse radii.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusX), RadiusX);
            OperationValidation.AddIfNotPositive(issues, nameof(RadiusY), RadiusY);
            return issues;
        }
    }
}

