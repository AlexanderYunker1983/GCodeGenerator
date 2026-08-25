#nullable enable
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>Диалог контура по эллипсу: центр, радиусы и поворот.</summary>
    public partial class ProfileEllipseOperationViewModel
        : ProfileOperationEditorViewModelBase<ProfileEllipseOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        public ProfileEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileEllipseName") ?? "ProfileEllipseName";
        }

        protected override bool IsValid(ProfileEllipseOperation operation)
            => operation.RadiusX > 0 && operation.RadiusY > 0 && operation.ToolDiameter > 0;
    }
}