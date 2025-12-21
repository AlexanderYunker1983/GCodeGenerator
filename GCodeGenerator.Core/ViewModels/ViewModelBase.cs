using System;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Инициализирует ресурсы и подписывается на изменение культуры
    /// </summary>
    protected void InitializeResources()
    {
        // Подписываемся на изменение культуры
        LocalizationService.Instance.CultureChanged += OnCultureChanged;
    }

    /// <summary>
    /// Обработчик изменения культуры. Может быть переопределен в дочерних классах для дополнительной логики
    /// </summary>
    protected virtual void OnCultureChanged(object? sender, EventArgs e)
    {
        // Автоматически обновляем все свойства с атрибутом [Localized]
        UpdateLocalizedProperties();
    }

    /// <summary>
    /// Обновляет все свойства, помеченные атрибутом [Localized]
    /// </summary>
    protected void UpdateLocalizedProperties()
    {
        var type = GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<LocalizedAttribute>() != null);

        foreach (var property in properties)
        {
            OnPropertyChanged(property.Name);
        }
    }

    /// <summary>
    /// Вызывается при закрытии окна, связанного с данной ViewModel.
    /// Может быть переопределен в дочерних классах для выполнения очистки ресурсов.
    /// Также может быть вызван вручную для вложенных ViewModel-ей.
    /// </summary>
    public virtual void OnViewClosed()
    {
        LocalizationService.Instance.CultureChanged -= OnCultureChanged;
        // Базовая реализация пустая, может быть переопределена в дочерних классах
    }
}
