using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по периметру прямоугольной сетки: внутренние узлы пропускаются.</summary>
    public partial class DrillRectOperationViewModel : DrillPatternEditorViewModelBase
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
        private int _holeCount = 4;

        [ObservableProperty]
        private double _angleDeg;

        [ObservableProperty]
        private double _rowPitch;

        [ObservableProperty]
        private int _rowCount = 2;

        public DrillRectOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillRect") ?? "AddDrillRect";
        }

        protected override DrillMode Mode => DrillMode.Rect;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            StartX = operation.StartX;
            StartY = operation.StartY;
            StartZ = operation.StartZ;
            Distance = operation.Distance;
            HoleCount = operation.HoleCount;
            AngleDeg = operation.AngleDeg;
            RowPitch = operation.RowPitch;
            RowCount = operation.RowCount;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.StartX = StartX;
            target.StartY = StartY;
            target.StartZ = StartZ;
            target.Distance = Distance;
            target.HoleCount = HoleCount;
            target.AngleDeg = AngleDeg;
            target.RowPitch = RowPitch;
            target.RowCount = RowCount;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}