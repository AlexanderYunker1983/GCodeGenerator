using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>Диалог эллиптического кармана: центр, радиусы и поворот.</summary>
    public partial class PocketEllipseOperationViewModel
        : PocketOperationEditorViewModelBase<PocketEllipseOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        public PocketEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketEllipseName") ?? "PocketEllipseName";
        }

        protected override bool IsValid()
            => Operation.RadiusX > 0 && Operation.RadiusY > 0 && Operation.ToolDiameter > 0 && Operation.StepPercentOfTool > 0;
    }
}