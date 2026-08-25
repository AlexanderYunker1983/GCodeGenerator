#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог контура по окружности: центр и радиус.</summary>
    public partial class ProfileCircleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileCircleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        public ProfileCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileCircleName") ?? "ProfileCircleName";
        }

    }
}