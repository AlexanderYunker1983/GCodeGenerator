using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Smoke-тесты инфраструктуры: ссылка на основную сборку, MSTest,
    /// адаптер для vstest и базовое поведение генератора.
    /// Полноценные golden-тесты добавляются в пункте 0.4 плана.
    /// </summary>
    [TestClass]
    public class SmokeTests
    {
        [TestMethod]
        public void Generator_ProducesProgram_EndingWithM30()
        {
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { X = 10, Y = 20, Z = 0, TotalDepth = 2, StepDepth = 1 } }
            };

            var program = new SimpleGCodeGenerator().Generate(
                new List<OperationBase> { operation }, new GCodeSettings { UseLineNumbers = false });

            Assert.IsTrue(program.Lines.Count > 0);
            Assert.AreEqual("M30", program.Lines[program.Lines.Count - 1]);
        }

        [TestMethod]
        public void Generator_Skips_Disabled_Operations()
        {
            var operation = new DrillPointsOperation
            {
                IsEnabled = false,
                Holes = { new DrillHole { X = 10, Y = 20, Z = 0, TotalDepth = 2, StepDepth = 1 } }
            };

            var program = new SimpleGCodeGenerator().Generate(
                new List<OperationBase> { operation }, new GCodeSettings { UseComments = true });

            Assert.IsFalse(program.Lines.Any(line => line.Contains(operation.Name)));
        }
    }
}
