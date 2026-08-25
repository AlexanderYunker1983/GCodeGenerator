using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation for rectangular pocket.
    /// </summary>
    public class PocketRectangleOperation : MillingOperationBase, IPocketOperation, IValidatable
    {
        public PocketRectangleOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket Rectangle")
        {
        }

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;
        public double Width { get; set; } = 10.0;

        public double Height { get; set; } = 10.0;

        public double RotationAngle { get; set; } = 0.0;

        public double ReferencePointX { get; set; } = 0.0;

        public double ReferencePointY { get; set; } = 0.0;

        public ReferencePointType ReferencePointType { get; set; } = ReferencePointType.Center;

        /// <summary>
        /// Pocketing step as percent of tool diameter (e.g., 40 => 40% of diameter).
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
            return $"Pocket rectangle {Width}x{Height}mm, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the pocket dimensions.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);
            OperationValidation.AddIfNotPositive(issues, nameof(Width), Width);
            OperationValidation.AddIfNotPositive(issues, nameof(Height), Height);
            return issues;
        }
    }
}


