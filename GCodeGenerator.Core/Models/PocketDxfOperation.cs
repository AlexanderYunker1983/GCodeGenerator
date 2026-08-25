using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation imported from DXF closed contours.
    /// </summary>
    public class PocketDxfOperation : PocketOperationBase, IValidatable
    {
        public PocketDxfOperation() : base(OperationType.PocketMilling, OperationCategory.Pocket, "Pocket DXF")
        {
        }

        public List<DxfPolyline> ClosedContours { get; set; } = new List<DxfPolyline>();

        public string DxfFilePath { get; set; }

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

