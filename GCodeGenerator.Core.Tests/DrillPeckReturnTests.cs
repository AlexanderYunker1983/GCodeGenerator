#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Возврат сверла в отверстие после отвода.
    ///
    /// Высота отвода задана абсолютной, поэтому чаще всего сверло между
    /// проходами выходит из отверстия целиком, а обратно идёт по всей
    /// пройденной глубине. Прежде этот путь целиком проходился быстрым
    /// ходом — до самого дна. Стружка, ради выброса которой отвод и делается,
    /// остаётся в отверстии всегда, и встречало её сверло на полной скорости.
    ///
    /// Проверяется на уровне траектории, а не текста программы: правило
    /// относится к движению инструмента и не зависит ни от постпроцессора,
    /// ни от числа знаков в координатах.
    /// </summary>
    [TestClass]
    public sealed class DrillPeckReturnTests
    {
        /// <summary>Допуск сравнения высот: траектория хранит числа без округления.</summary>
        private const double Tolerance = 1e-9;

        /// <summary>Верх отверстия у отверстия по умолчанию.</summary>
        private const double HoleTop = 0.0;

        /// <summary>
        /// Быстрый ход внутри отверстия останавливается не ниже, чем на зазор
        /// выше уже пройденной глубины.
        ///
        /// Проверка идёт по всей траектории: каждое быстрое перемещение,
        /// уходящее ниже верха отверстия, сравнивается с самой глубокой
        /// точкой, которую сверло к тому моменту прорезало.
        /// </summary>
        [TestMethod]
        public void RapidReturn_StopsAboveTheDepthAlreadyDrilled()
        {
            var path = Build(Hole(totalDepth: 6, stepDepth: 2));

            // Пока ничего не прорезано, быстрому ходу внутри отверстия делать
            // нечего вовсе: бесконечность отвергнет любую такую попытку.
            var drilled = double.NegativeInfinity;
            var returns = 0;

            foreach (var move in ZMoves(path))
            {
                var target = move.Z!.Value;

                if (move.Kind == ToolMoveKind.Rapid && target < HoleTop - Tolerance)
                {
                    returns++;
                    Assert.IsTrue(
                        target >= drilled + DrillPointsOperationGenerator.PeckReturnClearance - Tolerance,
                        $"возврат №{returns} пришёл на Z={target:0.###} при пройденных {drilled:0.###}");
                }
                else if (move.Kind == ToolMoveKind.Linear)
                {
                    drilled = Math.Min(drilled == double.NegativeInfinity ? target : drilled, target);
                }
            }

            Assert.AreEqual(2, returns, "Возвратов столько же, сколько отводов");
        }

        /// <summary>
        /// Возврат не встаёт между верхом отверстия и высотой отвода. Проход
        /// мельче зазора это позволял бы: сверло уходило бы обратно в воздух
        /// и проходило бы рабочей подачей путь, которого нет в материале.
        /// </summary>
        [TestMethod]
        public void ShallowStep_ClampsTheReturnToTheHoleTop()
        {
            const double holeZ = -3.0;
            const double retractZ = holeZ + 0.3;
            var step = DrillPointsOperationGenerator.PeckReturnClearance / 2;

            var path = Build(Hole(totalDepth: step * 4, stepDepth: step, holeZ: holeZ));
            var rapids = ZMoves(path).Where(move => move.Kind == ToolMoveKind.Rapid).ToList();

            foreach (var move in rapids)
            {
                Assert.IsFalse(
                    move.Z!.Value > holeZ + Tolerance && move.Z.Value < retractZ - Tolerance,
                    $"быстрый ход встал на Z={move.Z.Value:0.###} — между верхом отверстия и высотой отвода");
            }

            Assert.IsTrue(rapids.Count(move => Math.Abs(move.Z!.Value - holeZ) <= Tolerance) > 1,
                "Возврат упёрся в верх отверстия — иначе проверка выше ничего не значит");
        }

        /// <summary>
        /// Оставшийся участок сверло проходит рабочей подачей: быстрый ход
        /// обрывается выше пройденного, а не заменяет собой подачу.
        /// </summary>
        [TestMethod]
        public void ReturnEndsWithAWorkingFeedMove()
        {
            var path = Build(Hole(totalDepth: 6, stepDepth: 2));
            var moves = ZMoves(path);

            // Второй проход: отвод, возврат быстрым ходом, рез рабочей подачей.
            var retract = moves.FindIndex(move => Math.Abs(move.Z!.Value - 0.3) <= Tolerance);
            Assert.IsTrue(retract > 0, "Отвод между проходами есть");

            Assert.AreEqual(ToolMoveKind.Rapid, moves[retract + 1].Kind);
            Assert.AreEqual(-2 + DrillPointsOperationGenerator.PeckReturnClearance, moves[retract + 1].Z!.Value, Tolerance,
                "Быстрый ход остановился на зазор выше пройденного");

            Assert.AreEqual(ToolMoveKind.Linear, moves[retract + 2].Kind);
            Assert.AreEqual(-4, moves[retract + 2].Z!.Value, Tolerance, "Дальше — рабочая подача до конца прохода");
        }

        /// <summary>
        /// Отверстие в один проход возвратов не имеет: отводить нечего,
        /// и программа не меняется.
        /// </summary>
        [TestMethod]
        public void SinglePassHole_HasNoReturn()
        {
            var path = Build(Hole(totalDepth: 2, stepDepth: 2));
            var moves = ZMoves(path);

            Assert.AreEqual(1, moves.Count(move => move.Kind == ToolMoveKind.Linear),
                "Один рез — один проход");
            Assert.IsFalse(moves.Any(move => Math.Abs(move.Z!.Value - 0.3) <= Tolerance),
                "Отвода между проходами нет");
        }

        private static List<ToolMove> ZMoves(ToolPath path)
            => path.Moves().Where(move => move.Z.HasValue).ToList();

        private static ToolPath Build(OperationBase operation)
            => new SimpleGCodeGenerator().BuildToolPath(new[] { operation }, new GCodeSettings());

        private static DrillPointsOperation Hole(double totalDepth, double stepDepth, double holeZ = 0)
        {
            var operation = new DrillPointsOperation { DrillMode = DrillMode.Points };
            operation.Holes.Add(new DrillHole
            {
                X = 10,
                Y = 20,
                Z = holeZ,
                TotalDepth = totalDepth,
                StepDepth = stepDepth,
                FeedZRapid = 500,
                FeedZWork = 200,
                RetractHeight = holeZ + 0.3
            });
            operation.SafeZBetweenHoles = holeZ + 1;
            return operation;
        }
    }
}
