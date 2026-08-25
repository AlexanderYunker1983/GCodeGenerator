using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Trajectory;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Безопасный пролог программы.
    ///
    /// Единицы, система координат, плоскость обработки, режим подачи и
    /// активные коррекции — модальные состояния: стойка помнит их от
    /// предыдущей программы. Программа выводит абсолютные координаты
    /// в миллиметрах и подачи в мм/мин, но раньше нигде об этом не заявляла,
    /// поэтому после чужой программы в дюймах или в приращениях тот же файл
    /// давал совсем другую траекторию.
    /// </summary>
    [TestClass]
    public class SafetyPreambleTests
    {
        private static List<OperationBase> OneDrill()
            => new List<OperationBase>
            {
                new DrillPointsOperation
                {
                    Name = "Drill",
                    Holes = { new DrillHole { X = 10, Y = 20, TotalDepth = 2, StepDepth = 1 } }
                }
            };

        private static List<string> Generate(GCodeSettings settings)
            => new SimpleGCodeGenerator().Generate(OneDrill(), settings).Lines.ToList();

        /// <summary>Строка без номера: сравнение не зависит от нумерации.</summary>
        private static string WithoutLineNumber(string line)
        {
            if (!line.StartsWith("N"))
                return line;
            var space = line.IndexOf(' ');
            return space < 0 ? line : line.Substring(space + 1);
        }

        [TestMethod]
        public void Preamble_SetsModalStateBeforeAnyMove()
        {
            var lines = Generate(new GCodeSettings()).Select(WithoutLineNumber).ToList();

            Assert.AreEqual("G21 G90 G17 G94", lines[1],
                "Миллиметры, абсолютные координаты, плоскость XY, подача в минуту");
            Assert.AreEqual("G40 G49 G80", lines[2],
                "Отмена коррекции радиуса, коррекции длины и постоянного цикла");

            var firstMove = lines.FindIndex(line => line.StartsWith("G0") || line.StartsWith("G1"));
            Assert.IsTrue(firstMove > 2, "Пролог идёт до первого перемещения");
        }

        /// <summary>
        /// Пролог не зависит от настроек формата: он отвечает за безопасность,
        /// а не за оформление.
        /// </summary>
        [TestMethod]
        public void Preamble_IsEmittedWithCommentsAndNumbersDisabled()
        {
            var settings = new GCodeSettings();
            settings.Format.UseComments = false;
            settings.Format.UseLineNumbers = false;

            var lines = Generate(settings);

            Assert.IsTrue(lines.Contains("G21 G90 G17 G94"), "Пролог на месте без комментариев и номеров");
            Assert.IsTrue(lines.Contains("G40 G49 G80"), "Отмена коррекций на месте");
        }

        [TestMethod]
        public void Preamble_KeepsTwoDigitCodesWhenPadded()
        {
            var settings = new GCodeSettings();
            settings.Format.UsePaddedGCodes = true;

            var lines = Generate(settings).Select(WithoutLineNumber).ToList();

            Assert.AreEqual("G21 G90 G17 G94", lines[1], "Двузначные коды выравнивание не меняет");
        }

        /// <summary>
        /// Пролог не создаёт перемещений: траектория предпросмотра начинается
        /// там же, где начиналась раньше.
        /// </summary>
        [TestMethod]
        public void Preamble_AddsNoTrajectorySegments()
        {
            var program = new SimpleGCodeGenerator().Generate(OneDrill(), new GCodeSettings());
            var scene = SceneBuilder.Build(program);

            // Первое перемещение — подъём на безопасную высоту из нуля детали;
            // строки пролога координат не несут и сегментов не создают.
            Assert.IsTrue(scene.Segments.Count > 0, "Траектория построена");
            Assert.IsTrue(scene.Segments.All(segment => !Equals(segment.Start, segment.End)),
                "Пролог не добавил перемещений нулевой длины");
        }

        /// <summary>
        /// Система координат из настроек идёт после пролога: сначала общий
        /// режим станка, затем выбор нуля детали.
        /// </summary>
        [TestMethod]
        public void WorkCoordinateSystem_ComesAfterPreamble()
        {
            var settings = new GCodeSettings();
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G55";

            var lines = Generate(settings).Select(WithoutLineNumber).ToList();

            Assert.AreEqual("G40 G49 G80", lines[2], "Пролог на своём месте");
            Assert.AreEqual("G55", lines[3], "Система координат сразу после пролога");
        }
    }
}
