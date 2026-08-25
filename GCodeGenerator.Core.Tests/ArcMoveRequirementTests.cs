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
    /// без любой из пяти величин он не имеет смысла. Построитель траектории
    /// задаёт их всегда, но траектория может прийти и из чужого кода — тогда
    /// отсутствие величины должно называться, а не превращаться в исключение
    /// без единого указания на то, какая именно величина потерялась.
    /// </summary>
    [TestClass]
    public class ArcMoveRequirementTests
    {
        private static ToolPath PathWith(ToolMove move)
        {
            var operation = new ToolPathOperation("Дуга", "описание", 3);
            operation.Items.Add(move);

            var path = new ToolPath();
            path.Operations.Add(operation);
            return path;
        }

        private static GCodeProgram Build(ToolMove move)
            => new GenericPostProcessor().Build(PathWith(move), new GCodeSettings());

        [TestMethod]
        public void CompleteArc_IsWrittenAsProgram()
        {
            var move = new ToolMove(ToolMoveKind.ArcClockwise,
                x: 10, y: 20, z: null, centerOffsetX: 5, centerOffsetY: 0, feed: 300);

            var program = Build(move);

            Assert.IsTrue(program.Lines.Count > 0, "Полная дуга превращается в кадр");
        }

        /// <summary>
        /// Каждая недостающая величина названа по имени, а вместе с ней —
        /// вид перемещения: по такому сообщению видно, что искать.
        /// </summary>
        [TestMethod]
        public void ArcWithoutRequiredValue_IsRefusedByName()
        {
            var cases = new (string Name, ToolMove Move)[]
            {
                ("X", new ToolMove(ToolMoveKind.ArcClockwise, null, 20, null, 5, 0, 300)),
                ("Y", new ToolMove(ToolMoveKind.ArcClockwise, 10, null, null, 5, 0, 300)),
                ("CenterOffsetX", new ToolMove(ToolMoveKind.ArcClockwise, 10, 20, null, null, 0, 300)),
                ("CenterOffsetY", new ToolMove(ToolMoveKind.ArcClockwise, 10, 20, null, 5, null, 300)),
                ("Feed", new ToolMove(ToolMoveKind.ArcCounterClockwise, 10, 20, null, 5, 0, null)),
            };

            foreach (var (name, move) in cases)
            {
                var failure = Assert.Throws<InvalidOperationException>(() => Build(move));

                StringAssert.Contains(failure.Message, name, $"Сообщение должно называть {name}");
                StringAssert.Contains(failure.Message, move.Kind.ToString(), "и вид перемещения");
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
