using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>Диалог эллиптического кармана: центр, полуоси и поворот.</summary>
    public partial class PocketEllipseOperationViewModel
        : PocketOperationEditorViewModelBase<PocketEllipseOperation>, IHasDisplayName
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
        private double _radiusY = 10.0;

        [ObservableProperty]
        private double _rotationAngle;

        public PocketEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketEllipseName") ?? "PocketEllipseName";
        }

        protected override void LoadFromOperation(PocketEllipseOperation operation)
        {
            LoadCommonPocketParameters(operation);

            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            RadiusX = operation.RadiusX;
            RadiusY = operation.RadiusY;
            RotationAngle = operation.RotationAngle;
        }

        protected override void ApplyToOperation()
        {
            ApplyCommonPocketParameters(Operation);

            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.RadiusX = RadiusX;
            Operation.RadiusY = RadiusY;
            Operation.RotationAngle = RotationAngle;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid()
            => RadiusX > 0 && RadiusY > 0 && ToolDiameter > 0 && StepPercentOfTool > 0;
    }
}