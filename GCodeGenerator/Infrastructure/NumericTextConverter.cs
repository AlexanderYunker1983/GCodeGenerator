using System;
using System.Globalization;
using System.Windows.Data;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Преобразователь числового поля ввода.
    ///
    /// Стандартная привязка числа к текстовому полю в этой программе работала
    /// неверно сразу в двух отношениях. Культура привязок WPF по умолчанию —
    /// «en-US», а не культура пользователя, поэтому набранная запятая читалась
    /// как разделитель разрядов: «1,5» превращалось в 15 — глубина прохода
    /// в десять раз больше запрошенной, причём молча. Точку же поле не
    /// принимало по другой причине: «0.» — законное число 0, поэтому привязка
    /// обновляла значение и тут же переписывала текст обратно, стирая только
    /// что набранный разделитель.
    ///
    /// Здесь оба разделителя равноправны: и точка, и запятая означают дробную
    /// часть — в G-code разделитель всегда точка, а на клавиатуре пользователя
    /// чаще запятая. Незавершённый ввод («0.», «-», пустая строка) значение
    /// не меняет: пользователь ещё набирает.
    /// </summary>
    public sealed class NumericTextConverter : IValueConverter
    {
        /// <summary>Значение в текст: разделитель — точка, как в G-code.</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case double doubleValue:
                    return doubleValue.ToString(CultureInfo.InvariantCulture);
                case int intValue:
                    return intValue.ToString(CultureInfo.InvariantCulture);
                default:
                    return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Текст в значение. Незавершённый ввод оставляет значение прежним:
        /// <see cref="Binding.DoNothing"/> не трогает ни источник, ни поле,
        /// поэтому набранный разделитель остаётся на экране.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (value as string)?.Trim();
            if (string.IsNullOrEmpty(text))
                return Binding.DoNothing;

            // Набор ещё не закончен: «-», «0.» и «0,» — не значения, а
            // состояния на пути к нему. Разбирать их нельзя: «0.» —
            // законный ноль для двоичного разбора, и приняв его, привязка
            // переписала бы поле обратно в «0», стерев набранный разделитель.
            var last = text[text.Length - 1];
            if (last == '-' || last == '.' || last == ',')
                return Binding.DoNothing;

            // Оба разделителя означают дробную часть.
            var normalized = text.Replace(',', '.');

            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (underlyingType == typeof(int))
            {
                return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                    ? (object)intValue
                    : Binding.DoNothing;
            }

            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
                ? (object)doubleValue
                : Binding.DoNothing;
        }
    }
}
