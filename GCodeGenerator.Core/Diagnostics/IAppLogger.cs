#nullable enable
using System;

namespace GCodeGenerator.Diagnostics
{
    /// <summary>
    /// Уровень записи журнала.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>Информация о ходе работы (открытие/сохранение проекта, генерация).</summary>
        Info,

        /// <summary>Отклонение, которое не прерывает работу (отсутствующий ключ локализации).</summary>
        Warning,

        /// <summary>Сбой операции или необработанное исключение.</summary>
        Error
    }

    /// <summary>
    /// Журнал приложения. Ядро остаётся чистым BCL, поэтому контракт минимален,
    /// а конкретная реализация (файл, окно вывода, тестовый двойник) живёт
    /// в приложении и внедряется через IoC.
    ///
    /// Реализация обязана быть потокобезопасной и не бросать исключений:
    /// журнал — вспомогательная служба, её сбой не должен прерывать работу
    /// с проектом или генерацию G-code.
    /// </summary>
    public interface IAppLogger
    {
        /// <summary>Записывает сообщение в журнал.</summary>
        /// <param name="level">Уровень записи.</param>
        /// <param name="message">Текст сообщения.</param>
        /// <param name="exception">Исключение, если запись описывает сбой.</param>
        void Log(LogLevel level, string message, Exception? exception = null);
    }

    /// <summary>
    /// Сокращения для типовых вызовов <see cref="IAppLogger"/>.
    /// </summary>
    public static class AppLoggerExtensions
    {
        /// <summary>Записывает информационное сообщение.</summary>
        public static void Info(this IAppLogger logger, string message)
            => logger?.Log(LogLevel.Info, message);

        /// <summary>Записывает предупреждение.</summary>
        public static void Warning(this IAppLogger logger, string message)
            => logger?.Log(LogLevel.Warning, message);

        /// <summary>Записывает сбой вместе с исключением.</summary>
        public static void Error(this IAppLogger logger, string message, Exception? exception = null)
            => logger?.Log(LogLevel.Error, message, exception);
    }

    /// <summary>
    /// Журнал, который ничего не пишет. Значение по умолчанию для кода,
    /// который может работать без настроенного журнала (конструкторы для
    /// XAML-дизайнера, тесты, не проверяющие журналирование).
    /// </summary>
    public sealed class NullAppLogger : IAppLogger
    {
        /// <summary>Единственный экземпляр.</summary>
        public static readonly NullAppLogger Instance = new NullAppLogger();

        private NullAppLogger()
        {
        }

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception? exception = null)
        {
        }
    }
}
