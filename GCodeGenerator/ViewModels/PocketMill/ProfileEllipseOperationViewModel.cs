using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог обработки эллипса по контуру: центр, полуоси и поворот.</summary>
    public partial class ProfileEllipseOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileEllipseOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _radiusX = 15.0;

        [ObservableProperty]
        private double _radiusY = 8.0;

        [ObservableProperty]
        private double _rotationAngle;

        public ProfileEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileEllipseName") ?? "ProfileEllipseName";
        }

        protected override void LoadFromOperation(ProfileEllipseOperation operation)
        {
            LoadCommonProfileParameters(operation);

            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            RadiusX = operation.RadiusX;
            RadiusY = operation.RadiusY;
            RotationAngle = operation.RotationAngle;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonProfileParameters(Operation);

            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.RadiusX = RadiusX;
            Operation.RadiusY = RadiusY;
            Operation.RotationAngle = RotationAngle;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => RadiusX > 0 && RadiusY > 0 && ToolDiameter > 0;
    }
}