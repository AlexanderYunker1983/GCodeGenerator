using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Interfaces;

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
    public class ProfileDxfOperation : MillingOperationBase, IProfileOperation, IValidatable
    {
        public ProfileDxfOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile DXF")
        {
        }

        public List<DxfPolyline> Polylines { get; set; } = new List<DxfPolyline>();

        public string DxfFilePath { get; set; }

        /// <summary>
        /// Tool path mode: on line, outside, or inside contour.
        /// </summary>
        public ToolPathMode ToolPathMode { get; set; } = ToolPathMode.OnLine;

        /// <summary>
        /// Tool entry mode: vertical or angled.
        /// </summary>
        public EntryMode EntryMode { get; set; } = EntryMode.Vertical;

        /// <summary>
        /// Entry angle in degrees (for angled entry).
        /// </summary>
        public double EntryAngle { get; set; } = 5.0;

        /// <summary>
        /// Maximum segment length for arc approximation when arc support is disabled.
        /// </summary>
        public double MaxSegmentLength { get; set; } = 0.5;

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
            OperationValidation.AddCommonMillingIssues(issues, TotalDepth, StepDepth, ToolDiameter);

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


