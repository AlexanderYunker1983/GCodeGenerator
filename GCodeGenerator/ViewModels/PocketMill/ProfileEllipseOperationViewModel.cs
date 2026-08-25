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
        private string _displayName;

        public ProfileEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("ProfileEllipseName") ?? "ProfileEllipseName";
        }

        protected override bool IsValid()
            => Operation.RadiusX > 0 && Operation.RadiusY > 0 && Operation.ToolDiameter > 0;
    }
}