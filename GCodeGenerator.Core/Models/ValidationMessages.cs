using System;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Как проблема проверки превращается в текст для пользователя.
    ///
    /// Домен знает, что именно не так — код проблемы, имя параметра и предел, —
    /// но не знает языка окна. Приложение подставляет сюда перевод при запуске;
    /// без него остаётся английский текст, годный для журнала.
    /// </summary>
    public static class ValidationMessages
    {
        private static Func<ValidationIssue, string> _formatter = issue => issue?.Message ?? string.Empty;

        /// <summary>
        /// Преобразователь проблемы в текст. Задаётся приложением при запуске.
        /// </summary>
        public static Func<ValidationIssue, string> Formatter
        {
            get => _formatter;
            set => _formatter = value ?? (issue => issue?.Message ?? string.Empty);
        }

        /// <summary>Текст проблемы для показа пользователю.</summary>
        public static string Describe(ValidationIssue issue) => Formatter(issue);
    }
}
