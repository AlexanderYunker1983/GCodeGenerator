using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Неверные настройки отклоняются, а не исправляются молча.
    ///
    /// Прежде система координат вне диапазона просто не выводилась, а
    /// незнакомая команда пуска шпинделя заменялась на M3 — то есть
    /// «против часовой» могло стать «по часовой» без следа в программе,
    /// журнале и окне. Для кода, который поедет на станок, отказ безопаснее
    /// правдоподобной подмены.
    /// </summary>
    [TestClass]
    public class SettingsValidationTests
    {
        private static List<OperationBase> OneDrill()
            => new List<OperationBase>
            {
                new DrillPointsOperation
                {
                    Name = "Drill",
                    Holes = { new DrillHole { X = 1, Y = 1, TotalDepth = 2, StepDepth = 1 } }
                }
            };

        private static GCodeGenerationValidationException Generate(GCodeSettings settings)
            => Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(OneDrill(), settings));

        [TestMethod]
        public void UnknownWorkCoordinateSystem_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G99";

            var error = Generate(settings);

            var issue = error.SettingsIssues.Single(i => i.Property == "WorkCoordinateSystem");
            Assert.AreEqual(
                "must be one of G54, G55, G56, G57, G58, G59, but is \"G99\"",
                issue.Message,
                "Диагностика перечисляет допустимые системы и отвергнутое значение");
            StringAssert.Contains(error.Message, "G99", "В сообщении видно отвергнутое значение");
        }

        [TestMethod]
        public void EmptyWorkCoordinateSystem_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "";

            var error = Generate(settings);

            var issue = error.SettingsIssues.Single(i => i.Property == "WorkCoordinateSystem");
            Assert.AreEqual(
                "must be one of G54, G55, G56, G57, G58, G59, but is empty",
                issue.Message,
                "Пустая система координат при включённой настройке — тоже отказ");
        }

        [TestMethod]
        public void UnknownSpindleStartCommand_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleStartCommand = "M13";

            var error = Generate(settings);

            var issue = error.SettingsIssues.Single(i => i.Property == "SpindleStartCommand");
            Assert.AreEqual(
                "must be one of M3, M4, but is \"M13\"",
                issue.Message,
                "Диагностика перечисляет допустимые команды и отвергнутое значение");
            StringAssert.Contains(error.Message, "M13", "В сообщении видно отвергнутое значение");
        }

        [TestMethod]
        public void NullSettings_AreRejectedAtTheValidationBoundary()
        {
            var error = Assert.Throws<ArgumentNullException>(
                () => GCodeSettingsValidation.Validate(null!));

            Assert.AreEqual("settings", error.ParamName);
        }

        [TestMethod]
        public void MissingSettingsSections_AreAllReported()
        {
            var settings = new GCodeSettings
            {
                WorkCoordinate = null!,
                Format = null!,
                Spindle = null!,
                Coolant = null!,
                Machine = null!,
            };

            var issues = GCodeSettingsValidation.Validate(settings);
            var expected = new Dictionary<string, string>
            {
                [nameof(GCodeSettings.WorkCoordinate)] = "work-coordinate settings are missing",
                [nameof(GCodeSettings.Format)] = "format settings are missing",
                [nameof(GCodeSettings.Spindle)] = "spindle settings are missing",
                [nameof(GCodeSettings.Coolant)] = "coolant settings are missing",
                [nameof(GCodeSettings.Machine)] = "machine-profile settings are missing",
            };

            Assert.AreEqual(expected.Count, issues.Count,
                "Каждая обязательная секция даёт ровно одну самостоятельную причину отказа");
            foreach (var issue in issues)
            {
                Assert.AreEqual(ValidationCode.Empty, issue.Code, issue.Property);
                Assert.IsTrue(expected.TryGetValue(issue.Property, out var message), issue.Property);
                Assert.AreEqual(message, issue.Message, issue.Property);
            }
        }

        /// <summary>
        /// Файл проекта может принести незнакомую стойку — например, из более
        /// новой версии продукта. Отказ перечисляет допустимые и называет
        /// отвергнутое значение; молчаливая генерация «как для Generic» дала
        /// бы программу с неверной единицей паузы.
        /// </summary>
        [TestMethod]
        public void UnknownPostProcessor_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.Format.PostProcessorName = "Mazak";

            var error = Generate(settings);

            Assert.IsTrue(error.SettingsIssues.Any(i => i.Property == "PostProcessorName"),
                "Названа причина: стойка");
            StringAssert.Contains(error.Message, "Mazak", "В сообщении видно отвергнутое значение");
            StringAssert.Contains(error.Message, "Generic", "Перечислены допустимые стойки");
            StringAssert.Contains(error.Message, "GRBL", "Перечислены допустимые стойки");
        }

        /// <summary>
        /// Отказ перечисляет все причины сразу: и настройки, и операции.
        /// </summary>
        [TestMethod]
        public void SettingsAndOperationProblems_AreReportedTogether()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G99";

            var operations = new List<OperationBase>
            {
                new DrillPointsOperation { Name = "Без отверстий" }
            };

            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(operations, settings));

            Assert.AreEqual(1, error.SettingsIssues.Count, "Проблема настроек названа");
            Assert.AreEqual(1, error.Failures.Count, "Проблема операции названа");
        }

        [TestMethod]
        public void ValidSettings_AreAccepted()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "g55";
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleStartCommand = "m4";

            var program = new SimpleGCodeGenerator().Generate(OneDrill(), settings);

            Assert.IsTrue(program.Lines.Any(line => line.Contains("G55")), "Система координат выведена");
            Assert.IsTrue(program.Lines.Any(line => line.Contains("M4")), "Команда шпинделя выведена как задана");
        }

        /// <summary>
        /// Выключенные настройки не проверяются: незаполненное поле не мешает
        /// сгенерировать программу, которая его не использует.
        /// </summary>
        [TestMethod]
        public void DisabledSettings_AreNotValidated()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = false;
            settings.WorkCoordinate.WorkCoordinateSystem = "мусор";
            settings.Spindle.SpindleControlEnabled = false;
            settings.Spindle.SpindleStartCommand = "мусор";

            var program = new SimpleGCodeGenerator().Generate(OneDrill(), settings);

            Assert.IsTrue(program.Lines.Count > 0, "Программа построена");
        }

        [TestMethod]
        public void SpindleStartDisabled_IgnoresUnusedFiniteSpeedAndDelay()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = false;
            settings.Spindle.SpindleSpeedEnabled = true;
            settings.Spindle.SpindleSpeedRpm = int.MaxValue;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = GCodeSettingsValidation.MaxSpindleDelaySeconds + 100;

            var program = new SimpleGCodeGenerator().Generate(OneDrill(), settings);

            Assert.IsFalse(program.Lines.Any(line => line.Contains("G4 ")),
                "Неиспользуемая задержка не проверяется и не выводится");
            Assert.IsFalse(program.Lines.Any(line => line.Contains(" S")),
                "Неиспользуемые обороты не проверяются и не выводятся");
        }

        [TestMethod]
        public void DisabledSpindleDelay_StillRejectsNonFiniteValue()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = false;
            settings.Spindle.SpindleDelayEnabled = false;
            settings.Spindle.SpindleDelaySeconds = double.PositiveInfinity;

            var error = Generate(settings);
            var issue = error.SettingsIssues.Single(item =>
                item.Property == nameof(SpindleSettings.SpindleDelaySeconds));

            Assert.AreEqual(ValidationCode.NotFinite, issue.Code);
        }

        /// <summary>
        /// Задержка после пуска шпинделя задаётся в секундах, а выводится
        /// в миллисекундах: <c>G4 P</c> так понимают Fanuc-совместимые стойки.
        /// В GRBL и LinuxCNC тот же аргумент означает секунды, поэтому
        /// пересчёт зафиксирован тестом до появления профиля станка.
        /// </summary>
        [TestMethod]
        public void SpindleDelay_SecondsAreEmittedAsMilliseconds()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = 2.5;

            var lines = new SimpleGCodeGenerator().Generate(OneDrill(), settings).Lines;

            Assert.IsTrue(lines.Any(line => line.EndsWith("G4 P2500")),
                "2,5 секунды выводятся как G4 P2500");
        }

        /// <summary>
        /// Выключенная задержка команды не даёт — это состояние тоже стоит
        /// удерживать: лишний G4 останавливает станок на ровном месте.
        /// </summary>
        [TestMethod]
        public void SpindleDelay_Disabled_EmitsNoDwell()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleDelayEnabled = false;

            var lines = new SimpleGCodeGenerator().Generate(OneDrill(), settings).Lines;

            Assert.IsFalse(lines.Any(line => line.Contains("G4 ")), "Команды паузы в программе нет");
        }

        /// <summary>
        /// Обороты с лишним разрядом отклоняются. Проверялось только
        /// «не меньше одного», поэтому S200000 уходило в программу: таких
        /// шпинделей не существует, а стойка урезала бы значение до своего
        /// максимума и молча выполнила не то, что записано в проекте.
        /// </summary>
        [TestMethod]
        public void SpindleSpeed_AboveItsLimit_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleSpeedEnabled = true;
            settings.Spindle.SpindleSpeedRpm = GCodeSettingsValidation.MaxSpindleSpeedRpm + 1;

            var error = Generate(settings);

            var issue = error.SettingsIssues.Single(i => i.Property == "SpindleSpeedRpm");
            Assert.AreEqual(ValidationCode.AboveMaximum, issue.Code);
            Assert.AreEqual(GCodeSettingsValidation.MaxSpindleSpeedRpm, issue.Limit);
        }

        /// <summary>Паспортное значение самого быстрого шпинделя проходит.</summary>
        [TestMethod]
        public void SpindleSpeed_AtItsLimit_IsAccepted()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleSpeedEnabled = true;
            settings.Spindle.SpindleSpeedRpm = GCodeSettingsValidation.MaxSpindleSpeedRpm;

            var lines = new SimpleGCodeGenerator().Generate(OneDrill(), settings).Lines;

            Assert.IsTrue(lines.Any(line => line.Contains("S" + GCodeSettingsValidation.MaxSpindleSpeedRpm)),
                "Обороты выведены в программу");
        }

        /// <summary>
        /// Задержка после пуска шпинделя ограничена сверху: она нужна на
        /// разгон, это единицы секунд, а лишний разряд оставил бы станок
        /// стоять у выданной ему паузы.
        /// </summary>
        [TestMethod]
        public void SpindleDelay_AboveItsLimit_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.Spindle.SpindleControlEnabled = true;
            settings.Spindle.SpindleStartEnabled = true;
            settings.Spindle.SpindleDelayEnabled = true;
            settings.Spindle.SpindleDelaySeconds = GCodeSettingsValidation.MaxSpindleDelaySeconds + 1;

            var error = Generate(settings);

            Assert.IsTrue(error.SettingsIssues.Any(i => i.Property == "SpindleDelaySeconds"),
                "Названа причина: задержка после пуска шпинделя");
        }

        /// <summary>
        /// Построитель программы тоже не угадывает направление вращения:
        /// контракт один и тот же на обоих уровнях.
        /// </summary>
        [TestMethod]
        public void ProgramBuilder_RejectsUnknownSpindleCommand()
        {
            var builder = new ProgramBuilder(new GCodeProgram());

            Assert.Throws<System.ArgumentException>(() => builder.SpindleOn("M13"));
        }

        [TestMethod]
        public void MachineProfileMessages_UseInvariantNumbersUnderRussianCulture()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");

                var coordinates = new GCodeSettings();
                coordinates.Machine.Enabled = true;
                coordinates.Machine.MinX = 1.5;
                coordinates.Machine.MaxX = 10.5;
                coordinates.WorkCoordinate.AddStartPosition = true;
                coordinates.WorkCoordinate.StartX = 1.25;
                coordinates.WorkCoordinate.AddEndPosition = true;
                coordinates.WorkCoordinate.EndX = 10.75;

                var coordinateIssues = GCodeSettingsValidation.Validate(coordinates);
                Assert.AreEqual(
                    "must be at least the machine-profile limit 1.5, but is 1.25",
                    coordinateIssues.Single(issue => issue.Property == nameof(WorkCoordinateSettings.StartX)).Message);
                Assert.AreEqual(
                    "must be at most the machine-profile limit 10.5, but is 10.75",
                    coordinateIssues.Single(issue => issue.Property == nameof(WorkCoordinateSettings.EndX)).Message);

                var range = new GCodeSettings();
                range.Machine.Enabled = true;
                range.Machine.MinX = 1.5;
                range.Machine.MaxX = 1.25;
                var rangeIssue = GCodeSettingsValidation.Validate(range)
                    .Single(issue => issue.Property == nameof(MachineProfileSettings.MaxX));
                Assert.AreEqual(
                    "must be greater than MinX (1.5), but is 1.25",
                    rangeIssue.Message);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }
    }
}
