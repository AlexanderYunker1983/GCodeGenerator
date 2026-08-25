using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по окружности: центр, радиус, число отверстий и начальный угол.</summary>
    public partial class DrillCircleOperationViewModel : DrillPatternEditorViewModelBase
    {
        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _z;

        [ObservableProperty]
        private double _radius;

        [ObservableProperty]
        private int _holeCount = 2;

        [ObservableProperty]
        private double _startAngleDeg;

        public DrillCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillCircle") ?? "AddDrillCircle";
        }

        protected override DrillMode Mode => DrillMode.Circle;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            Radius = operation.Radius;
            HoleCount = operation.HoleCount;
            StartAngleDeg = operation.StartAngleDeg;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.Radius = Radius;
            target.HoleCount = HoleCount;
            target.StartAngleDeg = StartAngleDeg;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}