using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Точка контура. Обычные свойства без уведомлений: точки не правят
    /// поштучно — контур приходит из чертежа целиком, и об этом сообщает
    /// сама операция.
    /// </summary>
    public class DxfPoint
    {
        public double X { get; set; }

        public double Y { get; set; }
    }

    /// <summary>Ломаная контура; заполняется импортом чертежа целиком.</summary>
    public class DxfPolyline
    {
        public List<DxfPoint> Points { get; set; } = new List<DxfPoint>();
    }

    /// <summary>
    /// Profile milling operation imported from DXF lines.
    /// </summary>
    public partial class ProfileDxfOperation : ProfileOperationBase, IValidatable
    {
        public ProfileDxfOperation() : base(OperationType.ProfileMilling, OperationCategory.Profile, "Profile DXF")
        {
        }

        [ObservableProperty]

        private List<DxfPolyline> _polylines = new List<DxfPolyline>();

        [ObservableProperty]

        private string _dxfFilePath;

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


