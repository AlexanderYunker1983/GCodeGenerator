using System;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace GCodeGenerator.Core.Converters;

/// <summary>
/// Конвертирует флаг видимости левой панели в ширину столбца Grid.
/// </summary>
public class LeftPanelWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        if (value is bool isVisible)
        {
            return new GridLength(isVisible ? 200 : 0);
        }

        return new GridLength(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}


