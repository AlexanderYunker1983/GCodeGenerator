#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Перевод отказов ядра для показа пользователю.
    ///
    /// Ядро несёт код и аргументы (<see cref="CoreException"/>), интерфейс
    /// подставляет шаблон из словаря по ключу «CoreError_&lt;код&gt;» — та же
    /// схема, что у сообщений доменной проверки. Без перевода показывается
    /// нейтральное английское сообщение самого исключения: так же выглядят
    /// и все прочие исключения, у которых кода нет.
    /// </summary>
    public static class CoreErrorMessages
    {
        /// <summary>Текст отказа на языке интерфейса.</summary>
        /// <param name="exception">Отказ, который нужно показать.</param>
        /// <param name="localization">Словарь интерфейса; null — без перевода.</param>
        public static string Describe(Exception exception, ILocalizationManager? localization)
        {
            // Отказ проверки перед генерацией несёт разобранные проблемы,
            // а не только текст: он собирается заново на языке интерфейса.
            if (exception is GCodeGenerationValidationException validation && localization != null)
                return Describe(validation, localization);

            if (exception is CoreException core && localization != null)
            {
                var key = "CoreError_" + core.Code;
                var template = localization.GetString(key);

                // Отсутствующий ключ словарь возвращает как «?ключ?»:
                // это сигнал разработчику, пользователю честнее показать
                // нейтральное сообщение самого отказа.
                if (IsTranslated(template, key))
                    return string.Format(CultureInfo.CurrentCulture, template, core.Arguments.ToArray());
            }

            return exception.Message;
        }

        /// <summary>
        /// Текст проблемы параметра на языке интерфейса.
        ///
        /// Домен знает, что именно не так — код проблемы и предел, — но не
        /// знает языка окна. Отсюда же берут текст диалоги операций: через
        /// <see cref="ValidationMessages.Formatter"/>, который приложение
        /// направляет сюда при запуске.
        /// </summary>
        /// <param name="issue">Найденная проблема; null — пустая строка.</param>
        /// <param name="localization">Словарь интерфейса; null — без перевода.</param>
        public static string Describe(ValidationIssue? issue, ILocalizationManager? localization)
        {
            if (issue == null)
                return string.Empty;

            var key = "Validation." + issue.Code;
            var text = localization?.GetString(key, issue.LimitText);

            // Без перевода остаётся английский текст проблемы: он понятнее
            // ключа и годится для журнала.
            return IsTranslated(text, key) ? text! : issue.Message;
        }

        /// <summary>
        /// Отказ проверки перед генерацией — перечнем проблем, по строке на
        /// каждую, с заголовком настроек и каждой виновной операции.
        ///
        /// Само исключение собирает такой же перечень по-английски, но
        /// собирает его один раз и навсегда: он уходит в журнал, где язык
        /// интерфейса значения не имеет. Пользователю тот же перечень
        /// строится заново — тем же путём, что и в диалогах операций.
        /// </summary>
        /// <param name="failure">Отказ проверки перед генерацией.</param>
        /// <param name="localization">Словарь интерфейса.</param>
        private static string Describe(
            GCodeGenerationValidationException failure, ILocalizationManager localization)
        {
            var lines = new List<string>();

            if (failure.SettingsIssues.Count > 0)
            {
                lines.Add(Heading(localization, "GenerationValidationSettings", "Generation settings:"));
                lines.AddRange(failure.SettingsIssues.Select(issue => Line(issue, localization)));
            }

            foreach (var operation in failure.Failures)
            {
                // Тип операции есть в тексте исключения и уходит в журнал:
                // пользователю он ничего не говорит, а найти операцию в
                // списке хватает её места и имени.
                lines.Add(Heading(localization, "GenerationValidationOperation", "Operation #{0} \"{1}\":",
                    operation.OperationIndex + 1, operation.OperationName));
                lines.AddRange(operation.Issues.Select(issue => Line(issue, localization)));
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Проблема с отступом под своим заголовком.</summary>
        private static string Line(ValidationIssue issue, ILocalizationManager localization)
            => $"    {issue.Property}: {Describe(issue, localization)}";

        /// <summary>
        /// Заголовок перечня. Без перевода берётся английский образец: он
        /// разборчивее ключа, которым словарь отвечает на его отсутствие.
        /// </summary>
        /// <param name="localization">Словарь интерфейса.</param>
        /// <param name="key">Ключ словаря.</param>
        /// <param name="fallback">Английский образец с теми же подстановками.</param>
        /// <param name="arguments">Подстановки заголовка.</param>
        private static string Heading(
            ILocalizationManager localization, string key, string fallback, params object[] arguments)
        {
            var text = localization.GetString(key, arguments);
            return IsTranslated(text, key)
                ? text
                : string.Format(CultureInfo.CurrentCulture, fallback, arguments);
        }

        /// <summary>Словарь ответил переводом, а не «?ключ?» об его отсутствии.</summary>
        private static bool IsTranslated(string? text, string key)
            => !string.IsNullOrEmpty(text) && text != "?" + key + "?";
    }
}
