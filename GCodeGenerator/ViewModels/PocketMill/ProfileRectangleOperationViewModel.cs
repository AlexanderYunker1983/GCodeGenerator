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
        private string _displayName;

        public ProfileRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileRectangleName") ?? "ProfileRectangleName";
        }

        protected override bool IsValid()
            => Operation.Width > 0 && Operation.Height > 0 && Operation.ToolDiameter > 0;
    }
}