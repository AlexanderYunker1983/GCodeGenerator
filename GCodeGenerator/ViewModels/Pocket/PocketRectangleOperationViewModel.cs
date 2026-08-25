using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>Диалог прямоугольного кармана: размеры, поворот и точка привязки.</summary>
    public partial class PocketRectangleOperationViewModel
        : PocketOperationEditorViewModelBase<PocketRectangleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName;

        public PocketRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketRectangleName") ?? "PocketRectangleName";
        }

        protected override bool IsValid()
            => Operation.Width > 0 && Operation.Height > 0 && Operation.ToolDiameter > 0 && Operation.StepPercentOfTool > 0;
    }
}