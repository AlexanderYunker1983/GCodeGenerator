using System;
using GCodeGenerator.Models;
using GCodeGenerator.Properties;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Shared settings storage with a Properties.Settings persistence layer.
    /// The instance is owned by IoC; loading and saving use one mapping table
    /// (<see cref="SettingsMapping"/>) instead of duplicating every property.
    /// </summary>
    public sealed class AppSettingsStore : ISettingsStore
    {
        public event EventHandler SettingsChanged;

        public GCodeSettings Current { get; }

        public AppSettingsStore()
        {
            // Initialize from persistent storage (таблица SettingsMapping — пункт 8.1).
            Current = new GCodeSettings();
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in SettingsMapping.Entries)
                SettingsMapping.SetValue(Current, path, persisted[setting]);

            // Legacy-поведение: пустой WCS трактуется как G54.
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";
        }

        public void Save()
        {
            // Persist only fields that should survive restarts (та же таблица).
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in SettingsMapping.Entries)
                persisted[setting] = SettingsMapping.GetValue(Current, path);

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
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (!path.StartsWith("Ui.", StringComparison.Ordinal))
                    SettingsMapping.SetValue(Current, path, persisted[setting]);
            }
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
