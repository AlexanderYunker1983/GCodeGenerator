using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel : ViewModelBase, IHasDisplayName
{
    [ObservableProperty]
    private GCodeGenerator.Core.ViewModels.Preview2DViewModel.Preview2DViewModel _preview2DViewModel = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    public MainViewModel()
    {
        InitializeResources();
        
        // Подписываемся на изменения координат мыши в Preview2DViewModel
        Preview2DViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Preview2DViewModel.MouseWorldCoordinates))
            {
                UpdateStatusText();
            }
        };
    }
    
    private void UpdateStatusText()
    {
        var coords = Preview2DViewModel.MouseWorldCoordinates;
        if (coords.HasValue)
        {
            StatusText = string.Format(Resources.Status_Coordinates, coords.Value.X, coords.Value.Y);
        }
        else
        {
            StatusText = string.Empty;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsViewModel = WindowHelper.GetViewModel<GCodeGenerator.Core.ViewModels.SettingsViewModel.SettingsViewModel>();
        settingsViewModel.ShowDialog();
    }
}
