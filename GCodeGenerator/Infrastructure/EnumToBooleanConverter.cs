using System;
using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Data.Binding;

namespace GCodeGenerator.Infrastructure
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return value.Equals(parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Binding.DoNothing;

            if ((bool)value)
                return parameter;

            return Binding.DoNothing;
        }
    }
}

