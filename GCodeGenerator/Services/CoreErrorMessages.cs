#nullable enable
using System;
using System.Globalization;
using System.Linq;
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
            if (exception is CoreException core && localization != null)
            {
                var key = "CoreError_" + core.Code;
                var template = localization.GetString(key);

                // Отсутствующий ключ словарь возвращает как «?ключ?»:
                // это сигнал разработчику, пользователю честнее показать
                // нейтральное сообщение самого отказа.
                if (!string.IsNullOrEmpty(template) && template != "?" + key + "?")
                    return string.Format(CultureInfo.CurrentCulture, template, core.Arguments.ToArray());
            }

            return exception.Message;
        }
    }
}
