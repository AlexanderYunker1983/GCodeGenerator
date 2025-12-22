using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Interfaces;
using GCodeGenerator.Core.Localization;
using Preview2DViewModelNamespace = GCodeGenerator.Core.ViewModels.Preview2DViewModel;

namespace GCodeGenerator.Core.ViewModels.PropertiesViewModel;

/// <summary>
/// ViewModel панели свойств. Показывает свойства текущего выбранного объекта
/// (примитив или операция), помеченные атрибутом <see cref="PropertyEditorAttribute"/>.
/// </summary>
public partial class PropertiesViewModel : ViewModelBase, IHasDisplayName
{
    public ObservableCollection<PropertyItemViewModel> Properties { get; } = new();

    private readonly Preview2DViewModelNamespace.Preview2DViewModel _preview2DViewModel;

    public PropertiesViewModel(Preview2DViewModelNamespace.Preview2DViewModel preview2DViewModel)
    {
        _preview2DViewModel = preview2DViewModel;
        InitializeResources();
    }

    public string DisplayName => "Properties";

    /// <summary>
    /// Текущий объект, для которого показываются свойства.
    /// </summary>
    [ObservableProperty]
    private object? _selectedObject;

    partial void OnSelectedObjectChanged(object? value)
    {
        RebuildProperties();
    }

    /// <summary>
    /// Устанавливает новый источник свойств (примитив или операция).
    /// </summary>
    public void SetSource(object? source)
    {
        SelectedObject = source;
    }

    private void RebuildProperties()
    {
        Properties.Clear();

        if (SelectedObject is null)
            return;

        var type = SelectedObject.GetType();
        var props = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(p => (Property: p, Attr: p.GetCustomAttribute<PropertyEditorAttribute>()))
            .Where(t => t.Attr != null && t.Property.CanRead)
            .OrderBy(t => t.Attr!.Order)
            .ThenBy(t => t.Property.Name)
            .ToList();

        foreach (var (property, attr) in props)
        {
            var displayName = Resources.GetString(attr!.ResourceKey);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = property.Name;

            Properties.Add(new PropertyItemViewModel(
                SelectedObject,
                property,
                displayName,
                () => _preview2DViewModel.RequestRedraw()));
        }
    }

    /// <summary>
    /// Обновляет все свойства текущего объекта, вызывая уведомления об изменении.
    /// </summary>
    public void RefreshAllProperties()
    {
        foreach (var property in Properties)
        {
            property.Refresh();
        }
    }
}


