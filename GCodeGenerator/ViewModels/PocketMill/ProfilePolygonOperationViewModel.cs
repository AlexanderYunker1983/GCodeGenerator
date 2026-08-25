#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог контура по правильному многоугольнику.</summary>
    public partial class ProfilePolygonOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfilePolygonOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        public ProfilePolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfilePolygonName") ?? "ProfilePolygonName";
        }

        protected override bool IsValid(ProfilePolygonOperation operation)
            => operation.NumberOfSides >= 3 && operation.Radius > 0 && operation.ToolDiameter > 0;
    }
}