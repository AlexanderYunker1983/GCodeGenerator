using System.Reflection;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 8.1 плана: единственная таблица маппинга настроек
    /// «путь до свойства в <see cref="GCodeSettings"/> → имя в Properties.Settings».
    /// Используется <see cref="AppSettingsStore"/> и для загрузки, и для сохранения
    /// (ранее — ручная копия всех 28 свойств ×2).
    /// </summary>
    public static class SettingsMapping
    {
        private static readonly BindingFlags PropertyFlags = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>Таблица маппинга: путь в GCodeSettings → имя в Properties.Settings.</summary>
        public static readonly (string Path, string Setting)[] Entries =
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

        /// <summary>Читает значение свойства по пути (напр. "Spindle.SpindleSpeedRpm").</summary>
        public static object GetValue(GCodeSettings settings, string path)
        {
            object current = settings;
            foreach (var part in path.Split('.'))
                current = current.GetType().GetProperty(part, PropertyFlags).GetValue(current);
            return current;
        }

        /// <summary>Записывает значение свойства по пути (напр. "Spindle.SpindleSpeedRpm").</summary>
        public static void SetValue(GCodeSettings settings, string path, object value)
        {
            var parts = path.Split('.');
            object current = settings;
            for (var i = 0; i < parts.Length - 1; i++)
                current = current.GetType().GetProperty(parts[i], PropertyFlags).GetValue(current);
            current.GetType().GetProperty(parts[^1], PropertyFlags).SetValue(current, value);
        }
    }
}
