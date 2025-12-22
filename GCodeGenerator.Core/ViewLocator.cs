using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GCodeGenerator.Core.ViewModels;

namespace GCodeGenerator.Core;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var viewModelType = param.GetType();
        var viewModelName = viewModelType.FullName!;
        
        // Пробуем заменить "ViewModel" на "View" (приоритет для UserControl)
        var viewName = viewModelName.Replace("ViewModel", "View", StringComparison.Ordinal);
        
        // Ищем тип во всех загруженных сборках
        var type = FindType(viewName);

        if (type != null)
        {
            var control = (Control)Activator.CreateInstance(type)!;
            // Устанавливаем DataContext для созданного Control
            control.DataContext = param;
            return control;
        }

        return new TextBlock { Text = "Not Found: " + viewName };
    }

    private static Type? FindType(string fullName)
    {
        // Сначала пробуем Type.GetType (работает для типов в текущей сборке)
        var type = Type.GetType(fullName);
        if (type != null)
            return type;

        // Ищем во всех загруженных сборках
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(t => t.FullName == fullName);
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}