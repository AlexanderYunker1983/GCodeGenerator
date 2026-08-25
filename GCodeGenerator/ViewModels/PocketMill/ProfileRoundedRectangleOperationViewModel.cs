#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог контура по прямоугольнику со скруглёнными углами.</summary>
    public partial class ProfileRoundedRectangleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileRoundedRectangleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        public ProfileRoundedRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileRoundedRectangleName") ?? "ProfileRoundedRectangleName";
        }

    }
}