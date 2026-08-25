using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Диалог обработки прямоугольника со скруглёнными углами: размеры,
    /// поворот, радиус каждого угла и точка привязки.
    /// </summary>
    public partial class ProfileRoundedRectangleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileRoundedRectangleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _width = 40.0;

        [ObservableProperty]
        private double _height = 20.0;

        [ObservableProperty]
        private double _rotationAngle;

        [ObservableProperty]
        private double _radiusTopLeft = 2.0;

        [ObservableProperty]
        private double _radiusTopRight = 2.0;

        [ObservableProperty]
        private double _radiusBottomLeft = 2.0;

        [ObservableProperty]
        private double _radiusBottomRight = 2.0;

        [ObservableProperty]
        private double _referencePointX;

        [ObservableProperty]
        private double _referencePointY;

        [ObservableProperty]
        private ReferencePointType _referencePointType = ReferencePointType.Center;

        public ProfileRoundedRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileRoundedRectangleName")
                ?? "ProfileRoundedRectangleName";
        }

        protected override void LoadFromOperation(ProfileRoundedRectangleOperation operation)
        {
            LoadCommonProfileParameters(operation);

            Width = operation.Width;
            Height = operation.Height;
            RotationAngle = operation.RotationAngle;
            RadiusTopLeft = operation.RadiusTopLeft;
            RadiusTopRight = operation.RadiusTopRight;
            RadiusBottomLeft = operation.RadiusBottomLeft;
            RadiusBottomRight = operation.RadiusBottomRight;
            ReferencePointX = operation.ReferencePointX;
            ReferencePointY = operation.ReferencePointY;
            ReferencePointType = operation.ReferencePointType;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonProfileParameters(Operation);

            Operation.Width = Width;
            Operation.Height = Height;
            Operation.RotationAngle = RotationAngle;
            Operation.RadiusTopLeft = RadiusTopLeft;
            Operation.RadiusTopRight = RadiusTopRight;
            Operation.RadiusBottomLeft = RadiusBottomLeft;
            Operation.RadiusBottomRight = RadiusBottomRight;
            Operation.ReferencePointX = ReferencePointX;
            Operation.ReferencePointY = ReferencePointY;
            Operation.ReferencePointType = ReferencePointType;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => Width > 0 && Height > 0 && ToolDiameter > 0;
    }
}