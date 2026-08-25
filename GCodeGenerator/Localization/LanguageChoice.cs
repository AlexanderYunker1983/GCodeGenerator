#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace GCodeGenerator.Localization
{
    /// <summary>
    /// Язык интерфейса в списке выбора: код культуры и то, как он подписан.
    ///
    /// Название языка пишется на нём самом — «Русский», «English», — а не
    /// переводится: человек ищет в списке знакомое слово, и подпись на
    /// языке, которого он не знает, ему не поможет. Исключение — вариант
    /// «как в системе»: он про поведение программы, а не про язык, поэтому
    /// берётся из перевода и меняется вместе с ним.
    /// </summary>
    public sealed class LanguageChoice : INotifyPropertyChanged
    {
        /// <summary>Код языка системы: пустая строка.</summary>
        public const string SystemLanguage = "";

        /// <summary>Ключ подписи варианта «как в системе».</summary>
        private const string SystemLanguageKey = "SystemLanguage";

        private readonly string? _title;

        private LanguageChoice(string code, string? title)
        {
            Code = code;
            _title = title;

            if (code == SystemLanguage)
            {
                // Подпись этого варианта переводится, поэтому меняется
                // вместе с языком интерфейса.
                LocalizationSource.Instance.PropertyChanged += (_, __) =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
            }
        }

        /// <summary>Код культуры или пустая строка для языка системы.</summary>
        public string Code { get; }

        /// <summary>Подпись в списке.</summary>
        public string Title => _title ?? LocalizationSource.Instance[SystemLanguageKey];

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Языки, между которыми выбирает пользователь.</summary>
        public static IReadOnlyList<LanguageChoice> All { get; } = new[]
        {
            new LanguageChoice(SystemLanguage, null),
            new LanguageChoice("ru", "Русский"),
            new LanguageChoice("en", "English"),
        };

        /// <summary>
        /// Культура по коду языка. Пустой код и неизвестное значение дают
        /// культуру системы: файл настроек могли поправить руками, и такой
        /// случай должен вести к понятному поведению, а не к отказу.
        /// </summary>
        /// <param name="code">Код языка из настроек.</param>
        public static CultureInfo ToCulture(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return CultureInfo.CurrentUICulture;

            try
            {
                return CultureInfo.GetCultureInfo(code.Trim());
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentUICulture;
            }
        }
    }
}
