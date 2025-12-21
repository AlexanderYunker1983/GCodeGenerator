using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        InitializeResources();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = WindowHelper.GetViewModel<GCodeGenerator.Core.ViewModels.SettingsViewModel.SettingsViewModel>();
        settingsViewModel.ShowDialog();
    }
}
