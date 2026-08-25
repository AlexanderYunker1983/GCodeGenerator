using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по линии: начальная точка, шаг, число отверстий и угол линии.</summary>
    public partial class DrillLineOperationViewModel : DrillPatternEditorViewModelBase
    {
        [ObservableProperty]
        private double _startX;

        [ObservableProperty]
        private double _startY;

        [ObservableProperty]
        private double _startZ;

        [ObservableProperty]
        private double _distance;

        [ObservableProperty]
        private int _holeCount = 3;

        [ObservableProperty]
        private double _angleDeg;

        public DrillLineOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillLine") ?? "AddDrillLine";
        }

        protected override DrillMode Mode => DrillMode.Line;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            StartX = operation.StartX;
            StartY = operation.StartY;
            StartZ = operation.StartZ;
            Distance = operation.Distance;
            HoleCount = operation.HoleCount;
            AngleDeg = operation.AngleDeg;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.StartX = StartX;
            target.StartY = StartY;
            target.StartZ = StartZ;
            target.Distance = Distance;
            target.HoleCount = HoleCount;
            target.AngleDeg = AngleDeg;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}