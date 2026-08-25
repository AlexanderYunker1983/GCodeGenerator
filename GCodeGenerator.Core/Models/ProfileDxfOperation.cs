#nullable enable
using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Profile milling operation imported from DXF lines.
    /// </summary>
    public partial class ProfileDxfOperation : ProfileOperationBase, IValidatable
    {
        public ProfileDxfOperation() : base(OperationCategory.Profile, "Profile DXF")
        {
        }

        [ObservableProperty]

        private List<Polyline2D> _polylines = new List<Polyline2D>();

        [ObservableProperty]

        private string _dxfFilePath = string.Empty;

        public override string GetDescription()
        {
            var lines = 0;
            foreach (var poly in Polylines)
                lines += poly?.Points?.Count > 1 ? poly.Points.Count - 1 : 0;
            return $"DXF profile lines: {lines}";
        }

        /// <summary>
        /// Domain validation (plan item 3.7): common milling parameters and
        /// the imported polylines. Open polylines are legal for profile
        /// milling, so closedness is NOT required.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            OperationValidation.AddProfileIssues(issues, this);

            if (Polylines == null || Polylines.Count == 0)
            {
                issues.Add(new ValidationIssue(nameof(Polylines), "no polylines to mill"));
            }
            else
            {
                for (int i = 0; i < Polylines.Count; i++)
                {
                    var points = Polylines[i]?.Points;
                    if (points == null || points.Count < 2)
                        issues.Add(new ValidationIssue($"Polylines[{i}].Points", "a polyline needs at least 2 points"));
                }
            }

            return issues;
        }
    }
}


