using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по дуге: центр, радиус, число отверстий, начальный и конечный углы.</summary>
    public partial class DrillArcOperationViewModel : DrillPatternEditorViewModelBase
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

        [ObservableProperty]
        private double _endAngleDeg = 90;

        public DrillArcOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillArc") ?? "AddDrillArc";
        }

        protected override DrillMode Mode => DrillMode.Arc;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            Radius = operation.Radius;
            HoleCount = operation.HoleCount;
            StartAngleDeg = operation.StartAngleDeg;
            EndAngleDeg = operation.EndAngleDeg;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.Radius = Radius;
            target.HoleCount = HoleCount;
            target.StartAngleDeg = StartAngleDeg;
            target.EndAngleDeg = EndAngleDeg;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}