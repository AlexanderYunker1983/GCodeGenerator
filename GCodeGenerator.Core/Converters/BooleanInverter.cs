using System;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace GCodeGenerator.Core.Converters;

/// <summary>
/// Инвертирует булево значение, используется для скрытия/показа элементов в XAML.
/// </summary>
public class BooleanInverter : IValueConverter
{
    public static readonly BooleanInverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        if (value is bool b)
            return !b;
        return BindingOperations.DoNothing;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        if (value is bool b)
            return !b;
        return BindingOperations.DoNothing;
    }
}


