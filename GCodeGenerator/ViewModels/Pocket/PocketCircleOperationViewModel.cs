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

        public PocketCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketCircleName") ?? "PocketCircleName";
        }

        protected override bool IsValid()
            => Operation.Radius > 0 && Operation.ToolDiameter > 0 && Operation.StepPercentOfTool > 0;
    }
}
