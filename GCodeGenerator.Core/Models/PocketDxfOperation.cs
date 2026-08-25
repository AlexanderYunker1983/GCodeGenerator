using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation imported from DXF closed contours.
    /// </summary>
    public class PocketDxfOperation : MillingOperationBase, IPocketOperation, IValidatable
    {
        public PocketDxfOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket DXF")
        {
        }

        public List<DxfPolyline> ClosedContours { get; set; } = new List<DxfPolyline>();

        public string DxfFilePath { get; set; }

        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;

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
            var contours = ClosedContours?.Count ?? 0;
            return $"DXF pocket contours: {contours}, depth {TotalDepth}mm";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the imported closed contours. Unlike profile DXF, pocket contours
        /// MUST be closed (first and last points coincide within
        /// <see cref="OperationValidation.ContourClosedTolerance"/> — the same
        /// tolerance the DXF importer uses).
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);

            if (ClosedContours == null || ClosedContours.Count == 0)
            {
                issues.Add(new ValidationIssue(nameof(ClosedContours), "no closed contours to mill"));
            }
            else
            {
                for (int i = 0; i < ClosedContours.Count; i++)
                {
                    var contour = ClosedContours[i];
                    var points = contour?.Points;
                    if (points == null || points.Count < 3)
                    {
                        issues.Add(new ValidationIssue($"ClosedContours[{i}].Points", "a closed contour needs at least 3 points"));
                        continue;
                    }
                    if (!OperationValidation.IsContourClosed(contour))
                        issues.Add(new ValidationIssue($"ClosedContours[{i}]", "contour is not closed (first and last points differ)"));
                }
            }

            return issues;
        }
    }
}

