#nullable enable
using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Постоянное хранилище пользовательских настроек — то, что переживает
    /// перезапуск приложения.
    ///
    /// Граница нужна ради переноса настроек между версиями: <c>Properties.Settings</c>
    /// хранит файл в каталоге, привязанном к версии сборки, поэтому проверить
    /// перенос на реальном хранилище нельзя — версия задаётся при сборке.
    /// Через этот интерфейс перенос проверяется тестом на поддельном хранилище.
    /// </summary>
    internal interface IPersistedSettings
    {
        /// <summary>Значение настройки по имени из <see cref="SettingsMapping"/>.</summary>
        object this[string name] { get; set; }

        /// <summary>
        /// Настройки предыдущей версии ещё не перенесены. Признак сбрасывается
        /// после переноса и в дальнейшем читается из файла текущей версии.
        /// </summary>
        bool UpgradeRequired { get; set; }

        /// <summary>Переносит значения из файла настроек предыдущей версии.</summary>
        void Upgrade();

        /// <summary>Записывает настройки на диск.</summary>
        void Save();
    }

    /// <summary>
    /// Хранилище поверх <c>Properties.Settings</c> — файл
    /// <c>user.config</c> в профиле пользователя.
    /// </summary>
    internal sealed class ApplicationPersistedSettings : IPersistedSettings
    {
        private ApplicationSettingsBase _settings;

        internal ApplicationPersistedSettings(IAppLogger? logger = null)
            : this(
                logger ?? NullAppLogger.Instance,
                () => new Properties.Settings(),
                settings => _ = settings[nameof(Properties.Settings.UpgradeRequired)],
                () => DateTime.UtcNow)
        {
        }

        /// <summary>
        /// Конструктор с заменяемыми чтением и временем нужен проверке
        /// аварийного пути без вмешательства в настоящий профиль тестового
        /// пользователя.
        /// </summary>
        internal ApplicationPersistedSettings(
            IAppLogger logger,
            Func<ApplicationSettingsBase> settingsFactory,
            Action<ApplicationSettingsBase> probe,
            Func<DateTime> utcNow)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (settingsFactory == null)
                throw new ArgumentNullException(nameof(settingsFactory));
            if (probe == null)
                throw new ArgumentNullException(nameof(probe));
            if (utcNow == null)
                throw new ArgumentNullException(nameof(utcNow));

            _settings = settingsFactory();
            try
            {
                // ApplicationSettingsBase загружает user.config лениво:
                // создание экземпляра ещё не доказывает, что файл читается.
                probe(_settings);
            }
            catch (ConfigurationErrorsException failure)
            {
                var corruptPath = FindCorruptUserConfig(failure);
                if (corruptPath == null)
                    throw;

                var quarantinePath = Quarantine(corruptPath, utcNow());
                logger.Log(
                    LogLevel.Warning,
                    $"Corrupt user settings were moved from '{corruptPath}' to '{quarantinePath}'. "
                    + "The application will continue with default settings.",
                    failure);

                // Сломанное состояние могло остаться внутри экземпляра,
                // который уже начал ленивую загрузку. Новый экземпляр после
                // переноса файла читает только значения по умолчанию.
                _settings = settingsFactory();
                probe(_settings);
            }
        }

        public object this[string name]
        {
            get => _settings[name];
            set => _settings[name] = value;
        }

        public bool UpgradeRequired
        {
            get => (bool)_settings[nameof(Properties.Settings.UpgradeRequired)];
            set => _settings[nameof(Properties.Settings.UpgradeRequired)] = value;
        }

        public void Upgrade() => _settings.Upgrade();

        public void Save() => _settings.Save();

        private static string? FindCorruptUserConfig(ConfigurationErrorsException failure)
        {
            for (Exception? current = failure; current != null; current = current.InnerException)
            {
                if (current is not ConfigurationErrorsException configuration
                    || string.IsNullOrWhiteSpace(configuration.Filename))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(configuration.Filename);
                if (string.Equals(
                        Path.GetFileName(fullPath),
                        "user.config",
                        StringComparison.OrdinalIgnoreCase)
                    && File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private static string Quarantine(string corruptPath, DateTime utcNow)
        {
            var stamp = utcNow.ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
            var basePath = corruptPath + ".corrupt-" + stamp;
            var quarantinePath = basePath;
            for (var suffix = 1; File.Exists(quarantinePath); suffix++)
                quarantinePath = basePath + "-" + suffix.ToString(CultureInfo.InvariantCulture);

            File.Move(corruptPath, quarantinePath);
            return quarantinePath;
        }
    }
}
