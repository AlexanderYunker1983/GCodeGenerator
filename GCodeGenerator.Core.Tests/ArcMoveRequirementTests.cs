using System;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Обязательные величины дуги.
    ///
    /// Кадр G2/G3 описывается конечной точкой, смещением центра и подачей:
    /// без любой из пяти величин он не имеет смысла. Обязательность
    /// обеспечивает система типов: дуга представима только типом ArcMove,
    /// чей конструктор требует все пять величин, а попытка собрать дугу
    /// из обычного перемещения отклоняется при создании — раньше того же
    /// добивались проверки постпроцессора при выводе, и отказ приходил
    /// позже места, где дугу собрали.
    /// </summary>
    [TestClass]
    public class ArcMoveRequirementTests
    {
        private static ToolPath PathWith(ToolMove move)
        {
            var operation = new ToolPathOperation("Дуга", "описание", 3);
            var builder = new ToolPathBuilder(operation);
            if (move is ArcMove arc)
            {
                if (arc.Kind == ToolMoveKind.ArcClockwise)
                    builder.ArcCW(
                        arc.EndX, arc.EndY, arc.ArcCenterOffsetX, arc.ArcCenterOffsetY,
                        arc.ArcFeed, arc.EndZ);
                else
                    builder.ArcCCW(
                        arc.EndX, arc.EndY, arc.ArcCenterOffsetX, arc.ArcCenterOffsetY,
                        arc.ArcFeed, arc.EndZ);
            }
            else
            {
                builder.LinearTo(move.X, move.Y, move.Z, move.Feed);
            }

            var path = new ToolPath();
            path.AddOperation(operation);
            return path;
        }

        private static GCodeProgram Build(ToolMove move)
            => new GenericPostProcessor().Build(PathWith(move), new GCodeSettings());

        [TestMethod]
        public void CompleteArc_IsWrittenAsProgram()
        {
            var move = new ArcMove(clockwise: true, x: 10, y: 20, centerOffsetX: 5, centerOffsetY: 0, feed: 300);

            var program = Build(move);

            Assert.IsTrue(program.Lines.Count > 0, "Полная дуга превращается в кадр");
        }

        /// <summary>
        /// Дуга из обычного перемещения непредставима: пять величин дуги
        /// обязательны, и это выражено типом — отказ называет требуемый тип
        /// в момент создания, а не при выводе программы.
        /// </summary>
        [TestMethod]
        public void ArcAsPlainMove_IsRefusedAtConstruction()
        {
            foreach (var kind in new[] { ToolMoveKind.ArcClockwise, ToolMoveKind.ArcCounterClockwise })
            {
                var failure = Assert.Throws<ArgumentException>(
                    () => new ToolMove(kind, x: 10, y: 20, centerOffsetX: 5, centerOffsetY: 0, feed: 300));

                StringAssert.Contains(failure.Message, nameof(ArcMove),
                    "отказ называет тип, которым описывается дуга");
            }
        }

        /// <summary>
        /// У прямых перемещений тех же требований нет: кадр без координаты
        /// осмыслен — например, перемещение только по Z.
        /// </summary>
        [TestMethod]
        public void LinearMove_AllowsMissingCoordinates()
        {
            var move = new ToolMove(ToolMoveKind.Linear, x: null, y: null, z: -1, feed: 200);

            var program = Build(move);

            Assert.IsTrue(program.Lines.Count > 0, "Перемещение только по Z остаётся допустимым");
        }
    }
}
