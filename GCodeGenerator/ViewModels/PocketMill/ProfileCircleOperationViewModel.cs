using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог обработки окружности по контуру: центр и радиус.</summary>
    public partial class ProfileCircleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileCircleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _radius = 10.0;

        public ProfileCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileCircleName") ?? "ProfileCircleName";
        }

        protected override void LoadFromOperation(ProfileCircleOperation operation)
        {
            LoadCommonProfileParameters(operation);

            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Radius = operation.Radius;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonProfileParameters(Operation);

            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.Radius = Radius;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => Radius > 0 && ToolDiameter > 0;
    }
}