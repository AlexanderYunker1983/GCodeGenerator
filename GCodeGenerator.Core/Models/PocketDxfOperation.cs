#nullable enable
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Pocket milling operation imported from DXF closed contours.
    /// </summary>
    public partial class PocketDxfOperation : PocketOperationBase, IValidatable
    {
        public PocketDxfOperation() : base(OperationCategory.Pocket, "Pocket DXF")
        {
        }

        [ObservableProperty]

        private List<Polyline2D> _closedContours = new List<Polyline2D>();

        [ObservableProperty]

        private string _dxfFilePath = string.Empty;

        public override string GetDescription()
        {
            var contours = ClosedContours?.Count ?? 0;
            return Invariant($"DXF pocket contours: {contours}, depth {TotalDepth}mm");
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
            OperationValidation.AddPocketIssues(issues, this);

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
                    if (contour != null && !OperationValidation.IsContourClosed(contour))
                        issues.Add(new ValidationIssue($"ClosedContours[{i}]", "contour is not closed (first and last points differ)"));
                }
            }

            return issues;
        }
    }
}

