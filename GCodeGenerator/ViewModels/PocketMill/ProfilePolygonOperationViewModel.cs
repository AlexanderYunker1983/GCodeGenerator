using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Диалог обработки правильного многоугольника по контуру: центр, число
    /// сторон, радиус описанной окружности и поворот.
    /// </summary>
    public partial class ProfilePolygonOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfilePolygonOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private int _numberOfSides = 6;

        [ObservableProperty]
        private double _radius = 10.0;

        [ObservableProperty]
        private double _rotationAngle;

        public ProfilePolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfilePolygonName") ?? "ProfilePolygonName";
        }

        protected override void LoadFromOperation(ProfilePolygonOperation operation)
        {
            LoadCommonProfileParameters(operation);

            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            NumberOfSides = operation.NumberOfSides;
            Radius = operation.Radius;
            RotationAngle = operation.RotationAngle;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonProfileParameters(Operation);

            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.NumberOfSides = NumberOfSides;
            Operation.Radius = Radius;
            Operation.RotationAngle = RotationAngle;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => NumberOfSides >= 3 && Radius > 0 && ToolDiameter > 0;
    }
}