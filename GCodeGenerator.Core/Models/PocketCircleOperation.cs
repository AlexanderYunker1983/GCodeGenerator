using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for circular pocket.
    /// </summary>
    public class PocketCircleOperation : MillingOperationBase, IPocketOperation, IValidatable
    {
        public PocketCircleOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Circle")
        {
        }

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;

        public double CenterX { get; set; } = 0.0;

        public double CenterY { get; set; } = 0.0;

        public double Radius { get; set; } = 10.0;

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
            return $"Pocket circle R{Radius}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the pocket radius.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Radius), Radius);
            return issues;
        }
    }
}


