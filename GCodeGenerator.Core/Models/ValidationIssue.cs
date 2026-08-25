using System;
using System.Globalization;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Что именно не так с параметром. Код нужен интерфейсу: по нему
    /// подбирается сообщение на языке пользователя, тогда как текст
    /// <see cref="ValidationIssue.Message"/> одинаков и годится для журнала.
    /// </summary>
    public enum ValidationCode
    {
        /// <summary>Значение должно быть больше нуля.</summary>
        NotPositive,

        /// <summary>Значение должно быть не меньше указанного предела.</summary>
        BelowMinimum,

        /// <summary>Значение должно быть не больше указанного предела.</summary>
        AboveMaximum,

        /// <summary>Значение не является конечным числом.</summary>
        NotFinite,

        /// <summary>Значение отрицательно там, где это невозможно.</summary>
        Negative,

        /// <summary>Не задана геометрия: нет отверстий, контуров или полилиний.</summary>
        Empty,

        /// <summary>Контур не замкнут.</summary>
        ContourNotClosed,

        /// <summary>Значение не из списка допустимых.</summary>
        NotAllowed,

        /// <summary>Прочая несогласованность параметров.</summary>
        Inconsistent
    }

    /// <summary>
    /// A single problem found by domain validation (plan item 3.7).
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(string property, string message)
            : this(property, ValidationCode.Inconsistent, message)
        {
        }

        /// <summary>
        /// Проблема с кодом: интерфейс покажет её на языке пользователя,
        /// журнал — текстом.
        /// </summary>
        /// <param name="property">Имя параметра.</param>
        /// <param name="code">Что именно не так.</param>
        /// <param name="message">Текст для журнала и сообщений об отказе.</param>
        /// <param name="limit">Предел, если код о нём говорит.</param>
        public ValidationIssue(string property, ValidationCode code, string message, double? limit = null)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Code = code;
            Limit = limit;
        }

        /// <summary>
        /// Name of the property that failed validation (e.g. "StepDepth",
        /// "Holes[2].TotalDepth", "ClosedContours[0]").
        /// </summary>
        public string Property { get; }

        /// <summary>Что именно не так с параметром.</summary>
        public ValidationCode Code { get; }

        /// <summary>Предел, о котором говорит код, или <c>null</c>.</summary>
        public double? Limit { get; }

        /// <summary>
        /// Human-readable description of the problem.
        /// </summary>
        public string Message { get; }

        /// <summary>Имя параметра без индекса: «Holes[2].TotalDepth» → «TotalDepth».</summary>
        public string ParameterName
        {
            get
            {
                var name = Property;
                var dot = name.LastIndexOf('.');
                if (dot >= 0)
                    name = name.Substring(dot + 1);
                var bracket = name.IndexOf('[');
                return bracket >= 0 ? name.Substring(0, bracket) : name;
            }
        }

        public override string ToString() => $"{Property}: {Message}";

        /// <summary>Предел в виде текста для подстановки в сообщение.</summary>
        public string LimitText => Limit?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
