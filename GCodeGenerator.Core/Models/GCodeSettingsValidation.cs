#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Проверка настроек, влияющих на управляющую программу.
    ///
    /// Прежде неверные значения исправлялись молча: система координат вне
    /// диапазона просто не выводилась, а незнакомая команда пуска шпинделя
    /// заменялась на M3. Для кода, который поедет на станок, тихая подмена
    /// хуже отказа — «против часовой» превращалось в «по часовой» без следа
    /// в программе, журнале и окне.
    /// </summary>
    public static class GCodeSettingsValidation
    {
        /// <summary>Системы координат, которые понимает вывод программы.</summary>
        private static readonly string[] WorkCoordinateSystems =
            { "G54", "G55", "G56", "G57", "G58", "G59" };

        /// <summary>Команды пуска шпинделя: по часовой и против часовой.</summary>
        private static readonly string[] SpindleStartCommands = { "M3", "M4" };

        /// <summary>
        /// Возвращает все проблемы настроек; пустой список — программу можно
        /// строить.
        /// </summary>
        /// <param name="settings">Настройки генерации.</param>
        public static IReadOnlyList<ValidationIssue> Validate(GCodeSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var issues = new List<ValidationIssue>();

            var workCoordinate = settings.WorkCoordinate;
            if (workCoordinate != null && workCoordinate.SetWorkCoordinateSystem)
            {
                var wcs = (workCoordinate.WorkCoordinateSystem ?? string.Empty).Trim().ToUpperInvariant();
                if (Array.IndexOf(WorkCoordinateSystems, wcs) < 0)
                {
                    issues.Add(new ValidationIssue(
                        nameof(WorkCoordinateSettings.WorkCoordinateSystem),
                        $"must be one of {string.Join(", ", WorkCoordinateSystems)}, but is "
                        + (wcs.Length == 0 ? "empty" : $"\"{wcs}\"")));
                }
            }

            var format = settings.Format;
            if (format != null && format.UseLineNumbers)
            {
                // Нулевой шаг даёт программу, где каждая строка называется
                // одинаково: по такому номеру нельзя ни найти место, ни
                // продолжить с него обработку.
                OperationValidation.AddIfBelow(issues, nameof(GCodeFormatSettings.LineNumberStart),
                    format.LineNumberStart, 0);
                OperationValidation.AddIfBelow(issues, nameof(GCodeFormatSettings.LineNumberStep),
                    format.LineNumberStep, 1);
            }

            var spindle = settings.Spindle;
            if (spindle != null && spindle.SpindleControlEnabled)
            {
                if (spindle.SpindleSpeedEnabled)
                    OperationValidation.AddIfBelow(issues, nameof(SpindleSettings.SpindleSpeedRpm),
                        spindle.SpindleSpeedRpm, 1);

                if (spindle.SpindleDelayEnabled)
                    OperationValidation.AddIfNotPositive(issues, nameof(SpindleSettings.SpindleDelaySeconds),
                        spindle.SpindleDelaySeconds);
            }

            if (spindle != null && spindle.SpindleControlEnabled && spindle.SpindleStartEnabled)
            {
                var command = (spindle.SpindleStartCommand ?? string.Empty).Trim().ToUpperInvariant();
                if (Array.IndexOf(SpindleStartCommands, command) < 0)
                {
                    issues.Add(new ValidationIssue(
                        nameof(SpindleSettings.SpindleStartCommand),
                        $"must be one of {string.Join(", ", SpindleStartCommands)}, but is "
                        + (command.Length == 0 ? "empty" : $"\"{command}\"")));
                }
            }

            return issues;
        }
    }

    /// <summary>
    /// Проверка значений перечислений, пришедших из файла проекта.
    ///
    /// Перечисления сохраняются числами, поэтому файл может принести значение
    /// вне списка: стратегию выборки под номером 99 или несуществующий способ
    /// врезания. Раньше такое значение доходило до генератора и молча
    /// обрабатывалось как значение по умолчанию — программа получалась не той,
    /// что записана в проекте.
    /// </summary>
    public static class EnumValidation
    {
        /// <summary>Добавляет проблему, если значение не входит в перечисление.</summary>
        public static void AddIfUndefined<TEnum>(IList<ValidationIssue> issues, string property, TEnum value)
            where TEnum : struct, Enum
        {
            if (Enum.IsDefined(typeof(TEnum), value))
                return;

            issues.Add(new ValidationIssue(property, ValidationCode.NotAllowed,
                $"value {Convert.ToInt64(value)} is not a valid {typeof(TEnum).Name}"));
        }
    }
}
