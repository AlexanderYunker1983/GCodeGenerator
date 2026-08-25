#nullable enable
using System.Configuration;

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
        private readonly ApplicationSettingsBase _settings = Properties.Settings.Default;

        public object this[string name]
        {
            get => _settings[name];
            set => _settings[name] = value;
        }

        public bool UpgradeRequired
        {
            get => Properties.Settings.Default.UpgradeRequired;
            set => Properties.Settings.Default.UpgradeRequired = value;
        }

        public void Upgrade() => _settings.Upgrade();

        public void Save() => _settings.Save();
    }
}
