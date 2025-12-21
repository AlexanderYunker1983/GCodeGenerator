using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel : ViewModelBase, IHasDisplayName
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
