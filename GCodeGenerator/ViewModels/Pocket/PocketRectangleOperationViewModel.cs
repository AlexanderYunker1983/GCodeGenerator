#nullable enable
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
        private string _displayName = string.Empty;

        public PocketRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("PocketRectangleName") ?? "PocketRectangleName";
        }

    }
}