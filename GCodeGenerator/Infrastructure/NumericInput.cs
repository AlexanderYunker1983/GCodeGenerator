#nullable enable
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>Что разрешено вводить в числовое поле.</summary>
    public enum NumericInputMode
    {
        /// <summary>Поле не числовое: ввод не ограничивается.</summary>
        None,

        /// <summary>Целое число со знаком.</summary>
        Integer,

        /// <summary>Дробное число со знаком; разделитель — точка или запятая.</summary>
        Decimal
    }

    /// <summary>
    /// Ограничение ввода для числовых полей.
    ///
    /// Раньше в поле глубины или подачи можно было набрать что угодно: буквы
    /// оставались на экране, привязка молча отказывалась их принимать, и поле
    /// продолжало показывать текст, которого в операции нет. Пользователь
    /// закрывал диалог, считая, что задал значение.
    ///
    /// Здесь недопустимый символ просто не появляется в поле — ни с клавиатуры,
    /// ни вставкой из буфера обмена.
    /// </summary>
    public static class NumericInput
    {
        public static readonly DependencyProperty ModeProperty = DependencyProperty.RegisterAttached(
            "Mode",
            typeof(NumericInputMode),
            typeof(NumericInput),
            new PropertyMetadata(NumericInputMode.None, OnModeChanged));

        public static NumericInputMode GetMode(DependencyObject element)
            => (NumericInputMode)element.GetValue(ModeProperty);

        public static void SetMode(DependencyObject element, NumericInputMode value)
            => element.SetValue(ModeProperty, value);

        private static void OnModeChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (!(element is TextBox textBox))
                return;

            textBox.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(textBox, OnPaste);
            textBox.PreviewKeyDown -= OnPreviewKeyDown;

            if ((NumericInputMode)e.NewValue == NumericInputMode.None)
                return;

            textBox.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(textBox, OnPaste);
            textBox.PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>Пробел не печатает символ, но разрывает число — запрещаем явно.</summary>
        private static void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private static void OnPreviewTextInput(object? sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            e.Handled = !IsAllowed(textBox, ResultingText(textBox, e.Text));
        }

        private static void OnPaste(object? sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            var pasted = e.DataObject.GetDataPresent(DataFormats.UnicodeText)
                ? e.DataObject.GetData(DataFormats.UnicodeText) as string
                : null;

            if (pasted == null || !IsAllowed(textBox, ResultingText(textBox, pasted)))
                e.CancelCommand();
        }

        /// <summary>Каким станет текст поля после ввода.</summary>
        private static string ResultingText(TextBox textBox, string input)
        {
            var text = textBox.Text ?? string.Empty;
            var start = textBox.SelectionStart;
            var length = textBox.SelectionLength;
            return text.Substring(0, start) + input + text.Substring(start + length);
        }

        /// <summary>
        /// Допустим ли такой текст в поле. Промежуточные состояния набора
        /// («-», «0.», пустая строка) разрешены: иначе дробное число нельзя
        /// было бы набрать вовсе.
        /// </summary>
        internal static bool IsAllowed(TextBox textBox, string text)
            => IsAllowed(GetMode(textBox), text);

        internal static bool IsAllowed(NumericInputMode mode, string text)
        {
            if (mode == NumericInputMode.None)
                return true;

            if (string.IsNullOrEmpty(text))
                return true;

            var body = text;
            if (body[0] == '-')
                body = body.Substring(1);

            if (body.Length == 0)
                return true; // один минус — начало отрицательного числа

            if (mode == NumericInputMode.Integer)
                return body.All(char.IsDigit)
                       && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

            var separators = body.Count(IsDecimalSeparator);
            if (separators > 1)
                return false;

            if (!body.All(character => char.IsDigit(character) || IsDecimalSeparator(character)))
                return false;

            if (IsDecimalSeparator(body[body.Length - 1]))
                return true;

            var normalized = text.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                   && double.IsFinite(value);
        }

        /// <summary>
        /// Разделителем считается и точка, и запятая: в G-code он всегда
        /// точка, а на клавиатуре пользователя чаще запятая.
        /// </summary>
        private static bool IsDecimalSeparator(char character)
            => character == '.' || character == ','
               || CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == character.ToString();
    }
}
