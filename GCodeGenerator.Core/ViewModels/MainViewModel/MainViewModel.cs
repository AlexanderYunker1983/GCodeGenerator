using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Core.Helpers;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;
using GCodeGenerator.Core.Models;
using GCodeGenerator.Core.ViewModels.RightPanel.GCodeViewModel;
using GCodeGenerator.Core.ViewModels.RightPanel.OperationsListViewModel;
using GCodeGenerator.Core.ViewModels.RightPanel.PrimitivesListViewModel;
using GCodeGenerator.Core.ViewModels.PropertiesViewModel;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel : ViewModelBase, IHasDisplayName
{
    [ObservableProperty]
    private GCodeGenerator.Core.ViewModels.Preview2DViewModel.Preview2DViewModel _preview2DViewModel = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private PropertiesViewModel.PropertiesViewModel _propertiesViewModel;

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

        _propertiesViewModel = new PropertiesViewModel.PropertiesViewModel(Preview2DViewModel);

        InitializeRightPanelTabs();
        
        // Подписываемся на изменения координат мыши и подсветки примитивов в Preview2DViewModel
        Preview2DViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Preview2DViewModel.MouseWorldCoordinates) ||
                e.PropertyName == nameof(Preview2DViewModel.HoveredPrimitive))
            {
                UpdateStatusText();
            }

            if (e.PropertyName == nameof(Preview2DViewModel.SelectedPrimitive))
            {
                UpdatePropertiesSourceFromSelection();
            }
        };

        // Подписываемся на изменения свойств примитивов (например, при перетаскивании)
        Preview2DViewModel.PrimitivePropertyChanged += (s, e) =>
        {
            PropertiesViewModel.RefreshAllProperties();
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
        primitivesListViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PrimitivesListViewModel.SelectedPrimitive))
            {
                UpdatePropertiesSourceFromSelection();
            }
        };

        var operationsListViewModel = new OperationsListViewModel();
        operationsListViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(OperationsListViewModel.SelectedOperation))
            {
                UpdatePropertiesSourceFromSelection();
            }
        };

        RightPanelTabs.Add(primitivesListViewModel);
        RightPanelTabs.Add(operationsListViewModel);
        RightPanelTabs.Add(new GCodeViewModel());

        SelectedRightPanelTab = RightPanelTabs.Count > 0 ? RightPanelTabs[0] : null;
    }
    
    private void UpdateStatusText()
    {
        var coords = Preview2DViewModel.MouseWorldCoordinates;
        var hovered = Preview2DViewModel.HoveredPrimitive;

        string coordsText = coords.HasValue
            ? string.Format(Resources.Status_Coordinates, coords.Value.X, coords.Value.Y)
            : string.Empty;

        if (hovered != null && !string.IsNullOrEmpty(hovered.Name))
        {
            StatusText = string.IsNullOrEmpty(coordsText)
                ? hovered.Name
                : $"{coordsText} - {hovered.Name}";
        }
        else
        {
            StatusText = coordsText;
        }
    }

    private void UpdatePropertiesSourceFromSelection()
    {
        // Приоритет: выбранный примитив, затем выбранная операция.
        PrimitiveItem? selectedPrimitive = null;
        OperationItem? selectedOperation = null;

        foreach (var tab in RightPanelTabs)
        {
            if (tab is PrimitivesListViewModel primitivesVm)
            {
                selectedPrimitive = primitivesVm.SelectedPrimitive as PrimitiveItem ?? selectedPrimitive;
            }
            else if (tab is OperationsListViewModel operationsVm)
            {
                selectedOperation = operationsVm.SelectedOperation as OperationItem ?? selectedOperation;
            }
        }

        if (selectedPrimitive != null)
        {
            PropertiesViewModel.SetSource(selectedPrimitive);
        }
        else if (selectedOperation != null)
        {
            PropertiesViewModel.SetSource(selectedOperation);
        }
        else
        {
            PropertiesViewModel.SetSource(null);
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
