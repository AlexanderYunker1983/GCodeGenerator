#nullable enable
using System;
using System.Configuration;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Shared settings storage with a Properties.Settings persistence layer.
    /// The instance is owned by IoC; loading and saving use one mapping table
    /// (<see cref="SettingsMapping"/>) instead of duplicating every property.
    /// </summary>
    public sealed class AppSettingsStore : ISettingsStore
    {
        private readonly IPersistedSettings _persisted;

        public event EventHandler? SettingsChanged;

        public GCodeSettings Current { get; }

        public AppSettingsStore()
            : this(new ApplicationPersistedSettings())
        {
        }

        /// <summary>Хранилище настроек с заданным постоянным хранилищем (тесты).</summary>
        internal AppSettingsStore(IPersistedSettings persisted)
        {
            _persisted = persisted ?? throw new ArgumentNullException(nameof(persisted));
            UpgradeFromPreviousVersion(_persisted);

            // Initialize from persistent storage (таблица SettingsMapping — пункт 8.1).
            Current = new GCodeSettings();
            foreach (var (path, setting) in SettingsMapping.Entries)
                SettingsMapping.SetValue(Current, path, _persisted[setting]);

            // Legacy-поведение: пустой WCS трактуется как G54.
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";

            // Пустой ключ стойки — это отсутствие выбора, а не выбор «ничего»:
            // хранилище прежней версии ключа не содержит вовсе.
            if (string.IsNullOrEmpty(Current.Format.PostProcessorName))
                Current.Format.PostProcessorName = "Generic";
        }

        /// <summary>
        /// Переносит настройки из файла предыдущей версии программы.
        ///
        /// <c>Properties.Settings</c> хранит значения в каталоге, имя которого
        /// содержит версию сборки, а версия берётся из git-тега и меняется
        /// с каждым релизом. Без переноса очередное обновление показывало бы
        /// пользователю настройки по умолчанию, будто программу поставили заново.
        ///
        /// Признак <c>UpgradeRequired</c> лежит в самих настройках и по умолчанию
        /// истинен, поэтому файл новой версии начинает жизнь с переноса, а файл
        /// уже запускавшейся версии — нет. Признак сбрасывается после переноса:
        /// <c>Upgrade</c> копирует и его прежнее значение, поэтому порядок важен.
        ///
        /// Сбой переноса (нет файла предыдущей версии, повреждённый или
        /// недоступный <c>user.config</c>) не должен мешать запуску: настройки
        /// останутся значениями по умолчанию.
        /// </summary>
        private static void UpgradeFromPreviousVersion(IPersistedSettings persisted)
        {
            if (!persisted.UpgradeRequired)
                return;

            try
            {
                persisted.Upgrade();
                persisted.UpgradeRequired = false;
                persisted.Save();
            }
            catch (ConfigurationException)
            {
            }
        }

        public void Save()
        {
            // Persist only fields that should survive restarts (та же таблица).
            var persisted = _persisted;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                // Настройка без значения хранилищу не нужна: при чтении
                // вернётся значение по умолчанию из его описания.
                var value = SettingsMapping.GetValue(Current, path);
                if (value != null)
                    persisted[setting] = value;
            }

            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                persisted["WorkCoordinateSystem"] = "G54";
            persisted.Save();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Восстанавливает все настройки, влияющие на генерацию, из глобального
        /// хранилища. Группа Ui.* сохраняет текущее состояние приложения.
        /// </summary>
        public void RestoreGlobalGenerationSettings()
        {
            var persisted = _persisted;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (!path.StartsWith("Ui.", StringComparison.Ordinal))
                    SettingsMapping.SetValue(Current, path, persisted[setting]);
            }
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";
            if (string.IsNullOrEmpty(Current.Format.PostProcessorName))
                Current.Format.PostProcessorName = "Generic";
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
