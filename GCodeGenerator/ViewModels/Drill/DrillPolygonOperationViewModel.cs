using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>Диалог сверления по правильному многоугольнику: число сторон и отверстий на сторону.</summary>
    public partial class DrillPolygonOperationViewModel : DrillPatternEditorViewModelBase
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
        private int _numberOfSides = 6;

        [ObservableProperty]
        private int _holesPerSide = 2;

        [ObservableProperty]
        private double _rotationAngle;

        public DrillPolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("AddDrillPolygon") ?? "AddDrillPolygon";
        }

        protected override DrillMode Mode => DrillMode.Polygon;

        protected override void LoadPatternSpecificParameters(DrillPointsOperation operation)
        {
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            Radius = operation.Radius;
            NumberOfSides = operation.NumberOfSides;
            HolesPerSide = operation.HolesPerSide;
            RotationAngle = operation.RotationAngle;
        }

        protected override void ApplyPatternSpecificParameters(DrillPointsOperation target)
        {
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.Radius = Radius;
            target.NumberOfSides = NumberOfSides;
            target.HolesPerSide = HolesPerSide;
            target.RotationAngle = RotationAngle;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // шаблон без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}