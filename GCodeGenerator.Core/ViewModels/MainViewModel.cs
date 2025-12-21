using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;

namespace GCodeGenerator.Core.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = WindowHelper.GetViewModel<SettingsViewModel>();
        settingsViewModel.ShowDialog();
    }
}
