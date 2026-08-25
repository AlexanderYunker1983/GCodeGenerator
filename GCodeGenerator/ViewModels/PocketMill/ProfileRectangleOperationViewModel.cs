#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог контура по прямоугольнику: размеры, поворот и точка привязки.</summary>
    public partial class ProfileRectangleOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileRectangleOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        public ProfileRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileRectangleName") ?? "ProfileRectangleName";
        }

        protected override bool IsValid(ProfileRectangleOperation operation)
            => operation.Width > 0 && operation.Height > 0 && operation.ToolDiameter > 0;
    }
}