using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>Диалог круглого кармана: центр и радиус.</summary>
    public partial class PocketCircleOperationViewModel
        : PocketOperationEditorViewModelBase<PocketCircleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private double _centerX;

        [ObservableProperty]
        private double _centerY;

        [ObservableProperty]
        private double _radius = 10.0;

        public PocketCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketCircleName") ?? "PocketCircleName";
        }

        protected override void LoadFromOperation(PocketCircleOperation operation)
        {
            LoadCommonPocketParameters(operation);

            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Radius = operation.Radius;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonPocketParameters(Operation);

            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.Radius = Radius;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => Radius > 0 && ToolDiameter > 0 && StepPercentOfTool > 0;
    }
}
