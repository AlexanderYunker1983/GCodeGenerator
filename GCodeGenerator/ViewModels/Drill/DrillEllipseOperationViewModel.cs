using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по эллипсу: полуоси, поворот, число отверстий и начальный угол.</summary>
    public partial class DrillEllipseOperationViewModel : DrillPatternEditorViewModelBase
    {
        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _z;

        [ObservableProperty]
        private double _radiusX;

        [ObservableProperty]
        private double _radiusY;

        [ObservableProperty]
        private double _rotationAngle;

        [ObservableProperty]
        private int _holeCount = 2;

        [ObservableProperty]
        private double _startAngleDeg;

        public DrillEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillEllipse") ?? "AddDrillEllipse";
        }

        protected override DrillMode Mode => DrillMode.Ellipse;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            RadiusX = operation.RadiusX;
            RadiusY = operation.RadiusY;
            RotationAngle = operation.RotationAngle;
            HoleCount = operation.HoleCount;
            StartAngleDeg = operation.StartAngleDeg;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.RadiusX = RadiusX;
            target.RadiusY = RadiusY;
            target.RotationAngle = RotationAngle;
            target.HoleCount = HoleCount;
            target.StartAngleDeg = StartAngleDeg;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}