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
        private string _displayName;

        public ProfilePolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfilePolygonName") ?? "ProfilePolygonName";
        }

        protected override bool IsValid()
            => Operation.NumberOfSides >= 3 && Operation.Radius > 0 && Operation.ToolDiameter > 0;
    }
}