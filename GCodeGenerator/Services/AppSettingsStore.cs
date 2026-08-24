using System;
using System.Reflection;
using GCodeGenerator.Models;
using GCodeGenerator.Properties;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Shared settings storage with simple persistence layer (Properties.Settings).
    /// Пункт 7.5 плана: экземпляр через IoC (статический фасад <c>GCodeSettingsStore</c>
    /// остаётся [Obsolete] на один релиз). Пункт 8.1 плана: одна таблица маппинга
    /// «путь в GCodeSettings → имя в Properties.Settings» используется и для загрузки,
    /// и для сохранения (ранее — ручная копия всех 30 свойств ×2).
    /// </summary>
    public sealed class AppSettingsStore : ISettingsStore
    {
        private static readonly BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// Единственный источник маппинга настроек (пункт 8.1 плана):
        /// путь до свойства в <see cref="GCodeSettings"/> → имя в Properties.Settings.
        /// </summary>
        private static readonly (string Path, string Setting)[] Mapping =
        {
            ("Format.UseLineNumbers", "UseLineNumbers"),
            ("Format.LineNumberStart", "LineNumberStart"),
            ("Format.LineNumberStep", "LineNumberStep"),
            ("Format.UseComments", "UseComments"),
            ("Format.AllowArcs", "AllowArcs"),
            ("Format.UsePaddedGCodes", "UsePaddedGCodes"),
            ("Ui.UseDarkTheme", "UseDarkTheme"),
            ("Spindle.SpindleControlEnabled", "SpindleControlEnabled"),
            ("Spindle.SpindleSpeedEnabled", "SpindleSpeedEnabled"),
            ("Spindle.SpindleSpeedRpm", "SpindleSpeedRpm"),
            ("Spindle.SpindleStartEnabled", "SpindleStartEnabled"),
            ("Spindle.SpindleStartCommand", "SpindleStartCommand"),
            ("Spindle.SpindleStopEnabled", "SpindleStopEnabled"),
            ("Spindle.SpindleDelayEnabled", "SpindleDelayEnabled"),
            ("Spindle.SpindleDelaySeconds", "SpindleDelaySeconds"),
            ("Coolant.CoolantControlEnabled", "CoolantControlEnabled"),
            ("Coolant.CoolantStartEnabled", "CoolantStartEnabled"),
            ("Coolant.CoolantStopEnabled", "CoolantStopEnabled"),
            ("WorkCoordinate.AddStartPosition", "AddStartPosition"),
            ("WorkCoordinate.StartX", "StartX"),
            ("WorkCoordinate.StartY", "StartY"),
            ("WorkCoordinate.StartZ", "StartZ"),
            ("WorkCoordinate.AddEndPosition", "AddEndPosition"),
            ("WorkCoordinate.EndX", "EndX"),
            ("WorkCoordinate.EndY", "EndY"),
            ("WorkCoordinate.EndZ", "EndZ"),
            ("WorkCoordinate.SetWorkCoordinateSystem", "SetWorkCoordinateSystem"),
            ("WorkCoordinate.WorkCoordinateSystem", "WorkCoordinateSystem")
        };

        public GCodeSettings Current { get; }

        public AppSettingsStore()
        {
            // Initialize from persistent storage (одна таблица — пункт 8.1).
            Current = new GCodeSettings();
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in Mapping)
                SetPathValue(Current, path, persisted[setting]);

            // Legacy-поведение: пустой WCS трактуется как G54.
            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                Current.WorkCoordinate.WorkCoordinateSystem = "G54";
        }

        public void Save()
        {
            // Persist only fields that should survive restarts (та же таблица).
            var persisted = Properties.Settings.Default;
            foreach (var (path, setting) in Mapping)
                persisted[setting] = GetPathValue(Current, path);

            if (string.IsNullOrEmpty(Current.WorkCoordinate.WorkCoordinateSystem))
                persisted["WorkCoordinateSystem"] = "G54";
            persisted.Save();
        }

        private static object GetPathValue(GCodeSettings settings, string path)
        {
            object current = settings;
            foreach (var part in path.Split('.'))
                current = current.GetType().GetProperty(part, PropertyFlags).GetValue(current);
            return current;
        }

        private static void SetPathValue(GCodeSettings settings, string path, object value)
        {
            var parts = path.Split('.');
            object current = settings;
            for (var i = 0; i < parts.Length - 1; i++)
                current = current.GetType().GetProperty(parts[i], PropertyFlags).GetValue(current);
            current.GetType().GetProperty(parts[^1], PropertyFlags).SetValue(current, value);
        }
    }
}
