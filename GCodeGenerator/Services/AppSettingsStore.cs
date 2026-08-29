#nullable enable
using System;
using System.Configuration;
using System.Text.Json;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Shared settings storage with a Properties.Settings persistence layer.
    /// The instance is owned by IoC; loading and saving use one mapping table
    /// (<see cref="SettingsMapping"/>) instead of duplicating every property.
    ///
    /// Генерационные секции <see cref="Current"/> живут с документом;
    /// постоянное хранилище держит умолчания для новых проектов. Событие
    /// об изменении поднимается по фактической разнице, а не по вызову:
    /// прежде OK окна настроек всегда помечал проект несохранённым и
    /// сбрасывал программу, даже если менялась только тема.
    /// </summary>
    public sealed class AppSettingsStore : ISettingsStore
    {
        private readonly IPersistedSettings _persisted;

        /// <summary>
        /// Слепок генерационных секций <see cref="Current"/> на момент,
        /// когда о них в последний раз сообщалось. Сериализация — та же,
        /// что пишет файл проекта: изменение, которое не видит она,
        /// не видит и файл, а значит, о нём незачем и сообщать.
        /// </summary>
        private string _generationSnapshot;

        /// <summary>Последний сообщённый слепок локального профиля станка.</summary>
        private string _machineSnapshot;

        public event EventHandler? GenerationSettingsChanged;

        public event EventHandler? MachineProfileChanged;

        public GCodeSettings Current { get; }

        public AppSettingsStore()
            : this(new ApplicationPersistedSettings(NullAppLogger.Instance))
        {
        }

        /// <summary>Хранилище с журналом: сбой переноса настроек оставляет след.</summary>
        public AppSettingsStore(IAppLogger logger)
            : this(new ApplicationPersistedSettings(logger), logger)
        {
        }

        /// <summary>Хранилище настроек с заданным постоянным хранилищем (тесты).</summary>
        internal AppSettingsStore(IPersistedSettings persisted, IAppLogger? logger = null)
        {
            _persisted = persisted ?? throw new ArgumentNullException(nameof(persisted));
            UpgradeFromPreviousVersion(_persisted, logger ?? NullAppLogger.Instance);

            // Initialize from persistent storage (таблица SettingsMapping — пункт 8.1).
            Current = new GCodeSettings();
            foreach (var (path, setting) in SettingsMapping.Entries)
                SettingsMapping.SetValue(Current, path, _persisted[setting]);

            NormalizeCurrent();
            _generationSnapshot = GenerationSnapshot(Current);
            _machineSnapshot = MachineSnapshot(Current);
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
        /// останутся значениями по умолчанию, а причина — в журнале. Прежде
        /// сбой глотался молча, и «настройки сбросились после обновления»
        /// было не с чем сопоставить.
        /// </summary>
        private static void UpgradeFromPreviousVersion(IPersistedSettings persisted, IAppLogger logger)
        {
            if (!persisted.UpgradeRequired)
                return;

            try
            {
                persisted.Upgrade();
                persisted.UpgradeRequired = false;
                persisted.Save();
            }
            catch (ConfigurationException failure)
            {
                logger.Warning($"Settings upgrade from the previous version failed: {failure.Message}");
            }
        }

        public void Save()
        {
            // Пишутся только Ui-настройки и локальный профиль станка: генерационные принадлежат
            // документу, их копия в хранилище — умолчания новых проектов,
            // и меняет её только явная команда SaveGenerationDefaults.
            var persisted = _persisted;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (!path.StartsWith("Ui.", StringComparison.Ordinal)
                    && !path.StartsWith("Machine.", StringComparison.Ordinal))
                    continue;

                // Настройка без значения хранилищу не нужна: при чтении
                // вернётся значение по умолчанию из его описания.
                var value = SettingsMapping.GetValue(Current, path);
                if (value != null)
                    persisted[setting] = value;
            }

            persisted.Save();

            // Генерационные значения окно настроек к этому моменту уже
            // применило к Current — если они действительно изменились,
            // документ и программа должны об этом узнать.
            RaiseGenerationSettingsChangedIfNeeded();
        }

        public void SaveGenerationDefaults(GCodeSettings source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var persisted = _persisted;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (path.StartsWith("Ui.", StringComparison.Ordinal)
                    || path.StartsWith("Machine.", StringComparison.Ordinal))
                    continue;

                var value = SettingsMapping.GetValue(source, path);
                if (value != null)
                    persisted[setting] = value;
            }

            if (string.IsNullOrEmpty(source.WorkCoordinate.WorkCoordinateSystem))
                persisted["WorkCoordinateSystem"] = "G54";
            if (string.IsNullOrEmpty(source.Format.PostProcessorName))
                persisted["PostProcessorName"] = "Generic";
            persisted.Save();

            // Current не менялся: умолчания — отдельная копия, и событие
            // об их записи никому не нужно.
        }

        /// <summary>
        /// Восстанавливает все настройки, влияющие на генерацию, из глобального
        /// хранилища. Группа Ui.* сохраняет текущее состояние приложения.
        /// </summary>
        public void RestoreGlobalGenerationSettings()
        {
            RestoreGenerationFromPersisted();
            NormalizeCurrent();
            RaiseGenerationSettingsChangedIfNeeded();
        }

        public void ApplyProjectSettings(
            GCodeFormatSettings? format,
            SpindleSettings? spindle,
            CoolantSettings? coolant,
            WorkCoordinateSettings? workCoordinate)
        {
            // База — умолчания приложения: файл прежней версии может не
            // содержать какой-то секции, и она не должна унаследоваться
            // от предыдущего открытого проекта.
            RestoreGenerationFromPersisted();

            if (format != null)
                Current.Format = format;
            if (spindle != null)
                Current.Spindle = spindle;
            if (coolant != null)
                Current.Coolant = coolant;
            if (workCoordinate != null)
                Current.WorkCoordinate = workCoordinate;

            NormalizeCurrent();
            RaiseGenerationSettingsChangedIfNeeded();
        }

        /// <summary>Читает генерационные секции из постоянного хранилища в <see cref="Current"/>.</summary>
        private void RestoreGenerationFromPersisted()
        {
            var persisted = _persisted;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (!path.StartsWith("Ui.", StringComparison.Ordinal))
                    SettingsMapping.SetValue(Current, path, persisted[setting]);
            }
        }

        /// <summary>
        /// Пустые строковые настройки — это отсутствие выбора, а не выбор
        /// «ничего»: хранилище прежней версии не содержит их вовсе.
        /// </summary>
        private void NormalizeCurrent()
        {
            // Legacy-поведение: пустой WCS трактуется как G54.
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";
            if (string.IsNullOrEmpty(Current.Format.PostProcessorName))
                Current.Format.PostProcessorName = "Generic";
        }

        /// <summary>
        /// Сообщает об изменении генерационных настроек, только если они
        /// действительно изменились с прошлого сообщения.
        /// </summary>
        private void RaiseGenerationSettingsChangedIfNeeded()
        {
            var snapshot = GenerationSnapshot(Current);
            if (snapshot != _generationSnapshot)
            {
                _generationSnapshot = snapshot;
                GenerationSettingsChanged?.Invoke(this, EventArgs.Empty);
            }

            RaiseMachineProfileChangedIfNeeded();
        }

        private void RaiseMachineProfileChangedIfNeeded()
        {
            var snapshot = MachineSnapshot(Current);
            if (snapshot == _machineSnapshot)
                return;

            _machineSnapshot = snapshot;
            MachineProfileChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Слепок генерационных секций тем же сериализатором, что пишет файл
        /// проекта: равные слепки — равное содержимое будущего файла.
        /// </summary>
        private static string GenerationSnapshot(GCodeSettings settings)
            => JsonSerializer.Serialize(
                new
                {
                    settings.Format,
                    settings.Spindle,
                    settings.Coolant,
                    settings.WorkCoordinate,
                },
                ProjectJson.Options);

        private static string MachineSnapshot(GCodeSettings settings)
            => JsonSerializer.Serialize(settings.Machine, ProjectJson.Options);
    }
}
