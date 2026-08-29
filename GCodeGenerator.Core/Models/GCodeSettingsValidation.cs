#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>
        /// Наибольшие обороты шпинделя, об/мин.
        ///
        /// Шестьдесят тысяч — предел высокочастотных шпинделей, каких на
        /// фрезерном станке не бывает быстрее; у обычного фрезера потолок
        /// втрое ниже. Как и у подач, предел ловит лишний разряд: S200000
        /// проходило и уходило в программу.
        /// </summary>
        public const int MaxSpindleSpeedRpm = 60000;

        /// <summary>
        /// Наибольшая задержка после пуска шпинделя, с.
        ///
        /// Задержка нужна, чтобы шпиндель успел раскрутиться, — это единицы
        /// секунд. Минута заведомо больше любого разгона, а всё, что дольше,
        /// означает станок, стоящий у выданной ему паузы.
        /// </summary>
        public const double MaxSpindleDelaySeconds = 60.0;

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
            if (workCoordinate == null)
            {
                issues.Add(new ValidationIssue(nameof(GCodeSettings.WorkCoordinate), ValidationCode.Empty,
                    "work-coordinate settings are missing"));
            }
            else
            {
                if (workCoordinate.AddStartPosition)
                {
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.StartX), workCoordinate.StartX);
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.StartY), workCoordinate.StartY);
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.StartZ), workCoordinate.StartZ);
                }

                if (workCoordinate.AddEndPosition)
                {
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.EndX), workCoordinate.EndX);
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.EndY), workCoordinate.EndY);
                    OperationValidation.AddIfNotFinite(issues, nameof(WorkCoordinateSettings.EndZ), workCoordinate.EndZ);
                }
            }

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
            if (format == null)
                issues.Add(new ValidationIssue(nameof(GCodeSettings.Format), ValidationCode.Empty,
                    "format settings are missing"));
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
            if (spindle == null)
                issues.Add(new ValidationIssue(nameof(GCodeSettings.Spindle), ValidationCode.Empty,
                    "spindle settings are missing"));
            if (spindle != null && spindle.SpindleControlEnabled && spindle.SpindleStartEnabled)
            {
                if (spindle.SpindleSpeedEnabled)
                    OperationValidation.AddIfOutOfRange(issues, nameof(SpindleSettings.SpindleSpeedRpm),
                        spindle.SpindleSpeedRpm, 1, MaxSpindleSpeedRpm);

                if (spindle.SpindleDelayEnabled)
                    OperationValidation.AddIfOutOfPositiveRange(issues, nameof(SpindleSettings.SpindleDelaySeconds),
                        spindle.SpindleDelaySeconds, MaxSpindleDelaySeconds);
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

            if (settings.Coolant == null)
                issues.Add(new ValidationIssue(nameof(GCodeSettings.Coolant), ValidationCode.Empty,
                    "coolant settings are missing"));

            ValidateMachineProfile(settings.Machine, workCoordinate, spindle, issues);

            return issues;
        }

        private static void ValidateMachineProfile(
            MachineProfileSettings? machine,
            WorkCoordinateSettings? workCoordinate,
            SpindleSettings? spindle,
            IList<ValidationIssue> issues)
        {
            if (machine == null)
            {
                issues.Add(new ValidationIssue(nameof(GCodeSettings.Machine), ValidationCode.Empty,
                    "machine-profile settings are missing"));
                return;
            }

            if (!machine.Enabled)
                return;

            AddRangeIssues(issues, nameof(MachineProfileSettings.MinX), machine.MinX,
                nameof(MachineProfileSettings.MaxX), machine.MaxX);
            AddRangeIssues(issues, nameof(MachineProfileSettings.MinY), machine.MinY,
                nameof(MachineProfileSettings.MaxY), machine.MaxY);
            AddRangeIssues(issues, nameof(MachineProfileSettings.MinZ), machine.MinZ,
                nameof(MachineProfileSettings.MaxZ), machine.MaxZ);
            OperationValidation.AddIfOutOfPositiveRange(
                issues, nameof(MachineProfileSettings.MaxWorkFeed),
                machine.MaxWorkFeed, OperationValidation.MaxWorkFeed);
            OperationValidation.AddIfOutOfPositiveRange(
                issues, nameof(MachineProfileSettings.MaxRapidFeed),
                machine.MaxRapidFeed, OperationValidation.MaxRapidFeed);
            OperationValidation.AddIfOutOfRange(
                issues, nameof(MachineProfileSettings.MaxSpindleSpeedRpm),
                machine.MaxSpindleSpeedRpm, 1, MaxSpindleSpeedRpm);

            if (spindle != null
                && spindle.SpindleControlEnabled
                && spindle.SpindleStartEnabled
                && spindle.SpindleSpeedEnabled
                && spindle.SpindleSpeedRpm > machine.MaxSpindleSpeedRpm)
            {
                issues.Add(new ValidationIssue(
                    nameof(SpindleSettings.SpindleSpeedRpm),
                    ValidationCode.AboveMaximum,
                    $"must be at most the machine-profile limit {machine.MaxSpindleSpeedRpm}, but is {spindle.SpindleSpeedRpm}",
                    machine.MaxSpindleSpeedRpm));
            }

            if (workCoordinate?.AddStartPosition == true)
            {
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.StartX),
                    workCoordinate.StartX, machine.MinX, machine.MaxX);
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.StartY),
                    workCoordinate.StartY, machine.MinY, machine.MaxY);
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.StartZ),
                    workCoordinate.StartZ, machine.MinZ, machine.MaxZ);
            }

            if (workCoordinate?.AddEndPosition == true)
            {
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.EndX),
                    workCoordinate.EndX, machine.MinX, machine.MaxX);
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.EndY),
                    workCoordinate.EndY, machine.MinY, machine.MaxY);
                AddCoordinateIssue(issues, nameof(WorkCoordinateSettings.EndZ),
                    workCoordinate.EndZ, machine.MinZ, machine.MaxZ);
            }
        }

        private static void AddRangeIssues(
            IList<ValidationIssue> issues,
            string minProperty,
            double min,
            string maxProperty,
            double max)
        {
            OperationValidation.AddIfNotFinite(issues, minProperty, min);
            OperationValidation.AddIfNotFinite(issues, maxProperty, max);
            if (double.IsFinite(min) && double.IsFinite(max) && min >= max)
            {
                issues.Add(new ValidationIssue(
                    maxProperty,
                    ValidationCode.Inconsistent,
                    $"must be greater than {minProperty} ({min}), but is {max}"));
            }
        }

        private static void AddCoordinateIssue(
            IList<ValidationIssue> issues,
            string property,
            double value,
            double min,
            double max)
        {
            if (!double.IsFinite(value) || !double.IsFinite(min) || !double.IsFinite(max))
                return;

            if (value < min)
            {
                issues.Add(new ValidationIssue(property, ValidationCode.BelowMinimum,
                    $"must be at least the machine-profile limit {min}, but is {value}", min));
            }
            else if (value > max)
            {
                issues.Add(new ValidationIssue(property, ValidationCode.AboveMaximum,
                    $"must be at most the machine-profile limit {max}, but is {value}", max));
            }
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
            if (Enum.IsDefined(typeof(TEnum), value)
                || issues.Any(issue => issue != null && issue.Property == property))
                return;

            issues.Add(new ValidationIssue(property, ValidationCode.NotAllowed,
                $"value {Convert.ToInt64(value)} is not a valid {typeof(TEnum).Name}"));
        }
    }
}
