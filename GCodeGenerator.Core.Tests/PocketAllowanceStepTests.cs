using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Шаг между проходами при обработке с припуском.
    ///
    /// Припуск раньше задавался увеличением диаметра инструмента: операция
    /// клонировалась с диаметром больше настоящего на два припуска. Контур от
    /// этого получался правильный — траектория отступала от стенки ровно на
    /// припуск, — но шаг между соседними проходами тоже считается от диаметра,
    /// и брался он от несуществующей фрезы. При шаге 90% и припуске 1 мм фреза
    /// диаметром 6 мм получала шаг 7,2 мм: между соседними проходами оставалась
    /// нетронутая полоса материала шириной больше миллиметра.
    /// </summary>
    [TestClass]
    public class PocketAllowanceStepTests
    {
        private const double ToolDiameter = 6.0;
        private const double Allowance = 1.0;

        private static PocketCircleOperation Pocket()
            => new PocketCircleOperation
            {
                Name = "Карман",
                CenterX = 0,
                CenterY = 0,
                Radius = 30,
                ContourHeight = 0,
                TotalDepth = 1,
                StepDepth = 1,
                ToolDiameter = ToolDiameter,
                // Шаг у самой границы перекрытия: ошибка в диаметре сразу
                // разводит соседние проходы дальше, чем достаёт фреза.
                StepPercentOfTool = 90,
                PocketStrategy = PocketStrategy.Concentric,
                IsRoughingEnabled = true,
                FinishAllowance = Allowance,
            };

        /// <summary>
        /// Соседние проходы должны перекрываться: расстояние между ними не
        /// больше диаметра фрезы, иначе между ними остаётся материал.
        /// </summary>
        [TestMethod]
        public void RoughingWithAllowance_LeavesNoUncutMaterialBetweenPasses()
        {
            var radii = ConcentricPassRadii(Pocket());

            Assert.IsTrue(radii.Count > 2, "Проходов должно быть несколько");

            for (var i = 1; i < radii.Count; i++)
            {
                var gap = radii[i - 1] - radii[i];
                Assert.IsTrue(gap <= ToolDiameter + 1e-9,
                    $"Между проходами {radii[i - 1]:F3} и {radii[i]:F3} зазор {gap:F3} шире фрезы {ToolDiameter}");
            }
        }

        /// <summary>
        /// Шаг равен заданному проценту от настоящего диаметра, а не от
        /// диаметра с припуском: пользователь задаёт шаг для той фрезы,
        /// которая стоит в шпинделе.
        /// </summary>
        [TestMethod]
        public void RoughingWithAllowance_StepComesFromTheRealTool()
        {
            var operation = Pocket();
            var expectedStep = ToolDiameter * operation.StepPercentOfTool / 100.0;

            var radii = ConcentricPassRadii(operation);

            for (var i = 1; i < radii.Count - 1; i++)
            {
                // Последний проход обрывается на пороге вырождения контура,
                // поэтому сравниваются только полные шаги.
                Assert.AreEqual(expectedStep, radii[i - 1] - radii[i], 1e-6,
                    $"Шаг между проходами {i - 1} и {i}");
            }
        }

        /// <summary>
        /// Первый проход отстоит от стенки на радиус фрезы и припуск —
        /// материал у стенки остаётся для чистовой обработки.
        /// </summary>
        [TestMethod]
        public void RoughingWithAllowance_FirstPassKeepsAllowanceAtTheWall()
        {
            var operation = Pocket();

            var radii = ConcentricPassRadii(operation);

            // Окружность обходится многоугольником, поэтому радиус вершины
            // отличается от расчётного на доли микрона.
            Assert.AreEqual(
                operation.Radius - ToolDiameter / 2.0 - Allowance,
                radii[0],
                1e-3,
                "Крайний проход идёт по припуску, а не по стенке");
        }

        /// <summary>
        /// Радиусы концентрических проходов, от внешнего к внутреннему.
        /// Карман круглый, поэтому каждый проход — окружность, и расстояние
        /// от центра до любой её точки и есть радиус прохода.
        /// </summary>
        private static List<double> ConcentricPassRadii(PocketCircleOperation operation)
        {
            var toolPath = OperationToolPath.Build(
                new UnifiedPocketGenerator(), operation, new GCodeSettings());

            var radii = new SortedSet<double>();
            foreach (var move in toolPath.Moves())
            {
                if (move.Kind != ToolMoveKind.Linear || move.X == null || move.Y == null)
                    continue;

                var radius = Math.Sqrt(move.X.Value * move.X.Value + move.Y.Value * move.Y.Value);
                if (radius > 1e-6)
                    radii.Add(Math.Round(radius, 6));
            }

            return radii.Reverse().ToList();
        }
    }
}
