using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// Диалог обработки прямоугольника по контуру: размеры, поворот и точка
    /// привязки, относительно которой заданы размеры.
    /// </summary>
    public partial class ProfileRectangleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileRectangleOperation>, IHasDisplayName
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
        private double _referencePointX;

        [ObservableProperty]
        private double _referencePointY;

        [ObservableProperty]
        private ReferencePointType _referencePointType = ReferencePointType.Center;

        public ProfileRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileRectangleName") ?? "ProfileRectangleName";
        }

        protected override void LoadFromOperation(ProfileRectangleOperation operation)
        {
            LoadCommonProfileParameters(operation);

            Width = operation.Width;
            Height = operation.Height;
            RotationAngle = operation.RotationAngle;
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
            Operation.ReferencePointX = ReferencePointX;
            Operation.ReferencePointY = ReferencePointY;
            Operation.ReferencePointType = ReferencePointType;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => Width > 0 && Height > 0 && ToolDiameter > 0;
    }
}