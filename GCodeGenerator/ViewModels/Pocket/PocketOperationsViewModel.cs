using GCodeGenerator.Infrastructure;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketOperationsViewModel : ViewModelBase, IHasDisplayName
    {
        private string _displayName;

        public PocketOperationsViewModel(ILocalizationManager localizationManager)
        {
            var title = localizationManager?.GetString("PocketTab") ?? "Карман";
            DisplayName = title;
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }
    }
}


