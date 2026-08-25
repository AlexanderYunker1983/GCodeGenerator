using System.Collections.Generic;
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
            => Assert.ThrowsException<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(OneDrill(), settings));

        [TestMethod]
        public void UnknownWorkCoordinateSystem_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G99";

            var error = Generate(settings);

            Assert.IsTrue(error.SettingsIssues.Any(i => i.Property == "WorkCoordinateSystem"),
                "Названа причина: система координат");
            StringAssert.Contains(error.Message, "G99", "В сообщении видно отвергнутое значение");
        }

        [TestMethod]
        public void EmptyWorkCoordinateSystem_IsRejected()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "";

            var error = Generate(settings);

            Assert.IsTrue(error.SettingsIssues.Any(i => i.Property == "WorkCoordinateSystem"),
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

            Assert.IsTrue(error.SettingsIssues.Any(i => i.Property == "SpindleStartCommand"),
                "Названа причина: команда пуска шпинделя");
            StringAssert.Contains(error.Message, "M13", "В сообщении видно отвергнутое значение");
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

            var error = Assert.ThrowsException<GCodeGenerationValidationException>(
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

        /// <summary>
        /// Построитель программы тоже не угадывает направление вращения:
        /// контракт один и тот же на обоих уровнях.
        /// </summary>
        [TestMethod]
        public void ProgramBuilder_RejectsUnknownSpindleCommand()
        {
            var builder = new ProgramBuilder(new GCodeProgram());

            Assert.ThrowsException<System.ArgumentException>(() => builder.SpindleOn("M13"));
        }
    }
}
