#nullable enable
using System;
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
            ("Format.PostProcessorName", "PostProcessorName"),
            ("Ui.UseDarkTheme", "UseDarkTheme"),
            ("Ui.Language", "Language"),
            ("Ui.CheckForUpdates", "CheckForUpdates"),
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
        public static object? GetValue(GCodeSettings settings, string path)
        {
            object? current = settings;
            foreach (var part in path.Split('.'))
                current = Property(current, part, path).GetValue(current);
            return current;
        }

        /// <summary>Записывает значение свойства по пути (напр. "Spindle.SpindleSpeedRpm").</summary>
        public static void SetValue(GCodeSettings settings, string path, object? value)
        {
            var parts = path.Split('.');
            object? current = settings;
            for (var i = 0; i < parts.Length - 1; i++)
                current = Property(current, parts[i], path).GetValue(current);
            Property(current, parts[^1], path).SetValue(current, value);
        }

        /// <summary>
        /// Свойство по имени. Отсутствие свойства — ошибка самой таблицы,
        /// а не данных: путь в ней написан руками, и опечатка иначе дала бы
        /// отказ без указания, какая строка виновата.
        /// </summary>
        /// <param name="target">Объект, у которого ищется свойство.</param>
        /// <param name="name">Имя свойства.</param>
        /// <param name="path">Полный путь — для сообщения об ошибке.</param>
        private static PropertyInfo Property(object? target, string name, string path)
        {
            if (target == null)
                throw new InvalidOperationException($"Настройка «{path}»: путь обрывается на «{name}».");

            return target.GetType().GetProperty(name, PropertyFlags)
                ?? throw new InvalidOperationException(
                    $"Настройка «{path}»: у {target.GetType().Name} нет свойства «{name}».");
        }
    }
}
