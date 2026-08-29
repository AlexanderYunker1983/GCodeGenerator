using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// План обработки кармана: какие проходы и в каком порядке выполняются
    /// при черновой и чистовой обработке с припуском.
    ///
    /// Раньше эта последовательность была скрыта в методе с семью делегатами
    /// и проверялась только по готовому G-code. Теперь план строится отдельно,
    /// поэтому состав проходов и их параметры видны напрямую.
    /// </summary>
    [TestClass]
    public class PocketPassPlannerTests
    {
        [TestMethod]
        public void NoRoughingAndNoFinishing_GivesSinglePassOverOriginalOperation()
        {
            var operation = Pocket();

            var plan = PocketPassPlanner.Plan(operation);

            Assert.AreEqual(1, plan.Passes.Count);
            Assert.AreEqual(PocketPassKind.Pocketing, plan.Passes[0].Kind);
            Assert.AreSame(operation, plan.Passes[0].Operation, "без припуска копия операции не нужна");
            Assert.IsNull(plan.SkipComment);
        }

        [TestMethod]
        public void Roughing_LeavesAllowanceOnBottomAndWalls()
        {
            var operation = Pocket();
            operation.IsRoughingEnabled = true;
            operation.FinishAllowance = 0.4;

            var plan = PocketPassPlanner.Plan(operation);
            var rough = plan.Passes.Single();

            Assert.AreEqual(PocketPassKind.Pocketing, rough.Kind);
            Assert.AreEqual(4.6, rough.Operation.TotalDepth, 1e-9, "до дна остаётся припуск");
            Assert.AreEqual(0.4, rough.Allowance, 1e-9, "у стенки остаётся припуск");
            Assert.AreEqual(6.0, rough.Operation.ToolDiameter, 1e-9,
                "инструмент настоящий: от его диаметра считается шаг между проходами");
            Assert.AreEqual(5.0, operation.TotalDepth, 1e-9, "исходная операция не меняется");
            Assert.AreEqual(6.0, operation.ToolDiameter, 1e-9);
        }

        [TestMethod]
        public void ShallowRoughing_DoesNotClipWallAllowanceByDepth()
        {
            var operation = Pocket();
            operation.TotalDepth = 0.5;
            operation.IsRoughingEnabled = true;
            operation.FinishAllowance = 1.0;

            var rough = PocketPassPlanner.Plan(operation).Passes.Single();

            Assert.AreEqual(1.0, rough.Allowance, 1e-9,
                "Стеновой припуск в XY не зависит от глубины кармана");
            Assert.AreEqual(1e-6, rough.Operation.TotalDepth, 1e-9,
                "Припуск дна ограничивается глубиной и оставляет невырожденный черновой слой");
        }

        [TestMethod]
        public void Roughing_StopsWhenPocketDisappearsUnderAllowance()
        {
            var operation = Pocket();
            operation.Radius = 3.2;
            operation.IsRoughingEnabled = true;
            operation.IsFinishingEnabled = true;
            operation.FinishAllowance = 2.0;

            var plan = PocketPassPlanner.Plan(operation);

            Assert.AreEqual(0, plan.Passes.Count, "фрезеровать нечем");
            Assert.IsNotNull(plan.SkipComment);
        }

        /// <summary>
        /// Чистовая стенка проходит всю глубину кармана. Черновой проход
        /// отступает от стенки на припуск в каждом слое, поэтому припуск
        /// лежит на всей высоте стенки; прежний план ограничивал обход
        /// слоем припуска у дна — выше карман оставался уже задуманного
        /// на величину припуска.
        /// </summary>
        [TestMethod]
        public void FinishingWalls_RunsFullDepthAlongTheWall()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;
            operation.FinishingMode = PocketFinishingMode.Walls;
            operation.FinishAllowance = 0.4;

            var plan = PocketPassPlanner.Plan(operation);
            var walls = plan.Passes.Last();

            Assert.AreEqual(PocketPassKind.WallFinishing, walls.Kind);
            Assert.AreEqual(0.0, walls.Allowance, 1e-9, "стенка доводится начисто, без отступа");
            Assert.AreEqual(5.0, walls.Operation.TotalDepth, 1e-9, "обход контура на каждом слое до полной глубины");
            Assert.AreEqual(0.0, walls.Operation.ContourHeight, 1e-9, "с самого верха кармана");
            Assert.AreEqual(0.0, plan.TaperOriginZ, 1e-9, "уклон продолжает стенку исходного кармана");
        }

        [TestMethod]
        public void FinishingAll_CleansBottomBeforeWalls()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;
            operation.FinishingMode = PocketFinishingMode.All;
            operation.FinishAllowance = 0.4;

            var plan = PocketPassPlanner.Plan(operation);

            CollectionAssert.AreEqual(
                new[] { PocketPassKind.Pocketing, PocketPassKind.WallFinishing },
                plan.Passes.Select(p => p.Kind).ToArray());

            var bottom = plan.Passes[0];
            Assert.AreEqual(6.0, bottom.Operation.ToolDiameter, 1e-9, "инструмент настоящий");
            Assert.AreEqual(0.4, bottom.Allowance, 1e-9, "дно снимается с отступом от стенки");
            Assert.AreEqual(0.4, bottom.Operation.TotalDepth, 1e-9);
        }

        [TestMethod]
        public void RoughingAndFinishingBottom_GivesRoughThenBottomPass()
        {
            var operation = Pocket();
            operation.IsRoughingEnabled = true;
            operation.IsFinishingEnabled = true;
            operation.FinishingMode = PocketFinishingMode.Bottom;
            operation.FinishAllowance = 0.4;

            var plan = PocketPassPlanner.Plan(operation);

            Assert.AreEqual(2, plan.Passes.Count);
            Assert.IsTrue(plan.Passes.All(p => p.Kind == PocketPassKind.Pocketing), "стенка не дорабатывается");
            Assert.AreEqual(4.6, plan.Passes[0].Operation.TotalDepth, 1e-9);
            Assert.AreEqual(0.4, plan.Passes[1].Operation.TotalDepth, 1e-9);
        }

        [TestMethod]
        public void FinishingPasses_DoNotRequestFinishingAgain()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;
            operation.FinishAllowance = 0.4;

            var plan = PocketPassPlanner.Plan(operation);

            // Иначе исполнение прохода снова разложило бы его на проходы.
            Assert.IsTrue(plan.Passes.All(p => !p.Operation.IsRoughingEnabled && !p.Operation.IsFinishingEnabled));
        }

        private static PocketCircleOperation Pocket() => new PocketCircleOperation
        {
            CenterX = 0,
            CenterY = 0,
            Radius = 20,
            ContourHeight = 0,
            TotalDepth = 5,
            StepDepth = 1,
            ToolDiameter = 6
        };
    }
}
