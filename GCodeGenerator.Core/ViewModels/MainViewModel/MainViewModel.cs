using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;
using GCodeGenerator.Core.ViewModels.RightPanel.GCodeViewModel;
using GCodeGenerator.Core.ViewModels.RightPanel.OperationsListViewModel;
using GCodeGenerator.Core.ViewModels.RightPanel.PrimitivesListViewModel;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel : ViewModelBase, IHasDisplayName
{
    [ObservableProperty]
    private GCodeGenerator.Core.ViewModels.Preview2DViewModel.Preview2DViewModel _preview2DViewModel = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// Список вкладок правой панели.
    /// </summary>
    public ObservableCollection<ViewModelBase> RightPanelTabs { get; } = new();

    [ObservableProperty]
    private ViewModelBase? _selectedRightPanelTab;

    [ObservableProperty]
    private bool _isRightPanelVisible = true;

    [ObservableProperty]
    private bool _isLeftPanelVisible = true;

    /// <summary>
    /// Текст на кнопке сворачивания/разворачивания правой панели.
    /// </summary>
    public string RightPanelToggleText => IsRightPanelVisible ? ">>" : "<<";

    /// <summary>
    /// Текст на кнопке сворачивания/разворачивания левой панели.
    /// </summary>
    public string LeftPanelToggleText => IsLeftPanelVisible ? "<<" : ">>";

    public MainViewModel()
    {
        InitializeResources();

        InitializeRightPanelTabs();
        
        // Подписываемся на изменения координат мыши в Preview2DViewModel
        Preview2DViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Preview2DViewModel.MouseWorldCoordinates))
            {
                UpdateStatusText();
            }
        };
    }

    partial void OnIsRightPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(RightPanelToggleText));
    }

    partial void OnIsLeftPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(LeftPanelToggleText));
    }
    
    private void InitializeRightPanelTabs()
    {
        // Список примитивов разделяется между правой панелью и 2D-предпросмотром
        var primitivesListViewModel = new PrimitivesListViewModel(Preview2DViewModel);
        
        RightPanelTabs.Add(primitivesListViewModel);
        RightPanelTabs.Add(new OperationsListViewModel());
        RightPanelTabs.Add(new GCodeViewModel());

        SelectedRightPanelTab = RightPanelTabs.Count > 0 ? RightPanelTabs[0] : null;
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

    [RelayCommand]
    private void ToggleRightPanel()
    {
        IsRightPanelVisible = !IsRightPanelVisible;
    }

    [RelayCommand]
    private void ToggleLeftPanel()
    {
        IsLeftPanelVisible = !IsLeftPanelVisible;
    }
}
