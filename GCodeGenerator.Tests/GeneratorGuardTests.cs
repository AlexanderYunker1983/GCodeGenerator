using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты защитных проверок генераторов (пункт 3.8 плана).
    ///
    /// До 3.8 вырожденные параметры (StepDepth <= 0, ToolDiameter <= 0)
    /// приводили к бесконечным циклам в генераторах. Теперь они бросают
    /// ArgumentOutOfRangeException (производное от ArgumentException).
    /// Для валидных операций поведение не изменилось — это покрывают
    /// golden-тесты и тест ValidOperations_StillGenerate.
    /// </summary>
    [TestClass]
    public class GeneratorGuardTests
    {
        // ------------------------------------------------------------------
        // Вспомогательные методы
        // ------------------------------------------------------------------

        private static List<string> RunProfile(OperationBase op)
        {
            var lines = new List<string>();
            new UnifiedProfileGenerator().Generate(op, lines.Add, "G0", "G1", new GCodeSettings());
            return lines;
        }

        private static List<string> RunPocket(OperationBase op)
        {
            var lines = new List<string>();
            new UnifiedPocketGenerator().Generate(op, lines.Add, "G0", "G1", new GCodeSettings());
            return lines;
        }

        private static List<string> RunDrill(OperationBase op)
        {
            var lines = new List<string>();
            new DrillPointsOperationGenerator().Generate(op, lines.Add, "G0", "G1", new GCodeSettings());
            return lines;
        }

        // ------------------------------------------------------------------
        // Профили: StepDepth <= 0
        // ------------------------------------------------------------------

        [TestMethod]
        public void Profile_ZeroStepDepth_Throws()
        {
            var op = OperationFixtures.ProfileCircle();
            op.StepDepth = 0;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunProfile(op));
        }

        [TestMethod]
        public void Profile_NegativeStepDepth_Throws()
        {
            var op = OperationFixtures.ProfileCircle();
            op.StepDepth = -1;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunProfile(op));
        }

        // ------------------------------------------------------------------
        // Карманы: StepDepth <= 0 и ToolDiameter <= 0
        // ------------------------------------------------------------------

        [TestMethod]
        public void Pocket_ZeroStepDepth_Throws()
        {
            var op = OperationFixtures.PocketCircle();
            op.StepDepth = 0;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunPocket(op));
        }

        [TestMethod]
        public void Pocket_NegativeStepDepth_Throws()
        {
            var op = OperationFixtures.PocketCircle();
            op.StepDepth = -1;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunPocket(op));
        }

        [TestMethod]
        public void Pocket_ZeroToolDiameter_Throws()
        {
            // Нулевой диаметр → нулевой шаг спирали → бесконечный цикл до 3.8.
            var op = OperationFixtures.PocketCircle();
            op.ToolDiameter = 0;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunPocket(op));
        }

        [TestMethod]
        public void Pocket_NegativeToolDiameter_Throws()
        {
            var op = OperationFixtures.PocketCircle();
            op.ToolDiameter = -3;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunPocket(op));
        }

        // ------------------------------------------------------------------
        // Сверление: StepDepth <= 0 у отверстия
        // ------------------------------------------------------------------

        [TestMethod]
        public void Drill_ZeroStepDepthHole_Throws()
        {
            var op = OperationFixtures.DrillPoints();
            op.Holes[0].StepDepth = 0;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunDrill(op));
        }

        [TestMethod]
        public void Drill_NegativeStepDepthHole_Throws()
        {
            var op = OperationFixtures.DrillPoints();
            op.Holes[2].StepDepth = -0.5;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => RunDrill(op));
        }

        // ------------------------------------------------------------------
        // Хелпер: CalculateStep
        // ------------------------------------------------------------------

        [TestMethod]
        public void CalculateStep_ZeroDiameter_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => GCodeGenerationHelper.CalculateStep(0, 40));
        }

        [TestMethod]
        public void CalculateStep_NegativeDiameter_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => GCodeGenerationHelper.CalculateStep(-3, 40));
        }

        // ------------------------------------------------------------------
        // Регрессия: валидные операции генерируются без исключений
        // ------------------------------------------------------------------

        [TestMethod]
        public void ValidOperations_StillGenerate()
        {
            Assert.IsTrue(RunProfile(OperationFixtures.ProfileCircle()).Count > 0);
            Assert.IsTrue(RunPocket(OperationFixtures.PocketCircle()).Count > 0);
            Assert.IsTrue(RunDrill(OperationFixtures.DrillPoints()).Count > 0);
        }
    }
}
