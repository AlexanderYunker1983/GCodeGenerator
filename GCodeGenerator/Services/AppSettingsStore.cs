using GCodeGenerator.Models;
using GCodeGenerator.Properties;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Shared settings storage with simple persistence layer (Properties.Settings).
    /// Пункт 7.5 плана: экземпляр через IoC (статический фасад <c>GCodeSettingsStore</c>
    /// остаётся [Obsolete] на один релиз). Пункт 8.1 плана: загрузка/сохранение —
    /// по одной таблице маппинга (<see cref="SettingsMapping"/>, ранее — ручная копия
    /// всех 30 свойств ×2).
    /// </summary>
    public sealed class AppSettingsStore : ISettingsStore
    {
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
        }

        /// <summary>
        /// Пункт 8.2 плана (D4): шпиндель/СОЖ из персистентных глобальных значений.
        /// Та же таблица SettingsMapping, только строки Spindle.*/Coolant.*.
        /// </summary>
        public void RestoreGlobalSpindleAndCoolant()
        {
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in SettingsMapping.Entries)
            {
                if (path.StartsWith("Spindle.") || path.StartsWith("Coolant."))
                    SettingsMapping.SetValue(Current, path, persisted[setting]);
            }
        }
    }
}
