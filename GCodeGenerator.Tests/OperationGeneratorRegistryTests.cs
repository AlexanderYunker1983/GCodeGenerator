using System;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Юнит-тесты OperationGeneratorRegistry (пункт 4.5 плана): явный
    /// маппинг Type → IOperationGenerator вместо name-based рефлексии.
    /// </summary>
    [TestClass]
    public class OperationGeneratorRegistryTests
    {
        private static readonly OperationGeneratorRegistry Registry = new OperationGeneratorRegistry();

        private static readonly Type[] DrillTypes =
        {
            typeof(DrillPointsOperation),
        };

        private static readonly Type[] ProfileTypes =
        {
            typeof(ProfileCircleOperation),
            typeof(ProfileEllipseOperation),
            typeof(ProfilePolygonOperation),
            typeof(ProfileRectangleOperation),
            typeof(ProfileRoundedRectangleOperation),
            typeof(ProfileDxfOperation),
        };

        private static readonly Type[] PocketTypes =
        {
            typeof(PocketCircleOperation),
            typeof(PocketEllipseOperation),
            typeof(PocketRectangleOperation),
            typeof(PocketDxfOperation),
        };

        [TestMethod]
        public void All_11_Operation_Types_Are_Registered()
        {
            var all = DrillTypes.Concat(ProfileTypes).Concat(PocketTypes).ToArray();
            Assert.AreEqual(11, all.Length);
            foreach (var type in all)
            {
                Assert.IsTrue(Registry.TryGetGenerator(type, out _), $"Тип {type.Name} не зарегистрирован");
            }
        }

        [TestMethod]
        public void Drill_Types_Resolve_To_DrillPointsOperationGenerator()
        {
            foreach (var type in DrillTypes)
            {
                Assert.IsTrue(Registry.TryGetGenerator(type, out var generator), type.Name);
                Assert.IsInstanceOfType(generator, typeof(DrillPointsOperationGenerator));
            }
        }

        [TestMethod]
        public void Profile_Types_Resolve_To_UnifiedProfileGenerator()
        {
            foreach (var type in ProfileTypes)
            {
                Assert.IsTrue(Registry.TryGetGenerator(type, out var generator), type.Name);
                Assert.IsInstanceOfType(generator, typeof(UnifiedProfileGenerator));
            }
        }

        [TestMethod]
        public void Pocket_Types_Resolve_To_UnifiedPocketGenerator()
        {
            foreach (var type in PocketTypes)
            {
                Assert.IsTrue(Registry.TryGetGenerator(type, out var generator), type.Name);
                Assert.IsInstanceOfType(generator, typeof(UnifiedPocketGenerator));
            }
        }

        [TestMethod]
        public void Unknown_Types_Are_Not_Registered()
        {
            Assert.IsFalse(Registry.TryGetGenerator(typeof(OperationBase), out _));
            Assert.IsFalse(Registry.TryGetGenerator(typeof(object), out _));
            Assert.IsFalse(Registry.TryGetGenerator(typeof(string), out _));
        }

        /// <summary>
        /// Страховка от пропусков: каждый конкретный тип OperationBase в Core
        /// обязан иметь зарегистрированный генератор. Если появится новый тип
        /// операции, а маппинг в OperationGeneratorRegistry не обновят,
        /// тест упадёт (вместо молчаливого пропуска операции при генерации).
        /// </summary>
        [TestMethod]
        public void Every_Concrete_Operation_Type_In_Core_Has_A_Generator()
        {
            var operationTypes = typeof(OperationBase).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(OperationBase).IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToArray();

            Assert.IsTrue(operationTypes.Length >= 11, "Ожидалось не менее 11 типов операций");
            foreach (var type in operationTypes)
            {
                Assert.IsTrue(Registry.TryGetGenerator(type, out _),
                    $"Тип операции {type.Name} не зарегистрирован в OperationGeneratorRegistry");
            }
        }

        /// <summary>
        /// SimpleGCodeGenerator с явным реестром генерирует программу
        /// (реестр действительно используется в пайплайне).
        /// </summary>
        [TestMethod]
        public void SimpleGCodeGenerator_With_Explicit_Registry_Generates()
        {
            var generator = new SimpleGCodeGenerator(Registry);
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { X = 10, Y = 20, Z = 0, TotalDepth = 2, StepDepth = 1 } }
            };

            var program = generator.Generate(
                new System.Collections.Generic.List<OperationBase> { operation },
                new GCodeSettings { Format = new GCodeFormatSettings { UseLineNumbers = false } });

            Assert.IsTrue(program.Lines.Count > 0);
            Assert.AreEqual("M30", program.Lines[program.Lines.Count - 1]);
        }
    }
}
