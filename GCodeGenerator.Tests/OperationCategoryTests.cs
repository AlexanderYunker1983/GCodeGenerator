using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты категории операции (пункт 7.2 плана): каждый конкретный тип
    /// операции знает свою категорию (Drill/Profile/Pocket), категория не
    /// сериализуется в .ygc ([JsonIgnore]) и восстанавливается конструктором
    /// после round-trip.
    /// </summary>
    [TestClass]
    public class OperationCategoryTests
    {
        private static CultureInfo _originalCulture;

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CultureInfo.CurrentCulture = _originalCulture;
        }

        private static ProjectFileService Service { get; } = new ProjectFileService();

        /// <summary>
        /// Все 11 конкретных типов операций (покрыто фикстурами 0.3) имеют
        /// ожидаемую категорию.
        /// </summary>
        [TestMethod]
        public void AllConcreteOperations_HaveExpectedCategory()
        {
            var expected = new Dictionary<Type, OperationCategory>
            {
                { typeof(DrillPointsOperation), OperationCategory.Drill },
                { typeof(ProfileCircleOperation), OperationCategory.Profile },
                { typeof(ProfileEllipseOperation), OperationCategory.Profile },
                { typeof(ProfilePolygonOperation), OperationCategory.Profile },
                { typeof(ProfileRectangleOperation), OperationCategory.Profile },
                { typeof(ProfileRoundedRectangleOperation), OperationCategory.Profile },
                { typeof(ProfileDxfOperation), OperationCategory.Profile },
                { typeof(PocketCircleOperation), OperationCategory.Pocket },
                { typeof(PocketEllipseOperation), OperationCategory.Pocket },
                { typeof(PocketRectangleOperation), OperationCategory.Pocket },
                { typeof(PocketDxfOperation), OperationCategory.Pocket }
            };

            // Страховка от пропусков: список типов совпадает с реальными
            // конкретными классами OperationBase в Core.
            var actualTypes = new List<Type>();
            foreach (var t in typeof(OperationBase).Assembly.GetTypes())
            {
                if (t.IsClass && !t.IsAbstract && typeof(OperationBase).IsAssignableFrom(t))
                    actualTypes.Add(t);
            }
            CollectionAssert.AreEquivalent(expected.Keys.ToList(), actualTypes,
                "Список конкретных операций изменился — обновите ожидание");

            foreach (var (type, category) in expected)
            {
                var op = (OperationBase)Activator.CreateInstance(type);
                Assert.AreEqual(category, op.Category, $"{type.Name}: Category");
            }
        }

        /// <summary>
        /// Багфикс 7.2a: карманы (кроме DXF) раньше проходили как
        /// OperationType.ProfileMilling — теперь PocketMilling.
        /// </summary>
        [TestMethod]
        public void PocketOperations_HavePocketMillingOperationType()
        {
            Assert.AreEqual(OperationType.PocketMilling, new PocketCircleOperation().Type);
            Assert.AreEqual(OperationType.PocketMilling, new PocketEllipseOperation().Type);
            Assert.AreEqual(OperationType.PocketMilling, new PocketRectangleOperation().Type);
            Assert.AreEqual(OperationType.PocketMilling, new PocketDxfOperation().Type);
        }

        /// <summary>
        /// Category не пишется в .ygc ([JsonIgnore]) и восстанавливается
        /// конструктором после загрузки.
        /// </summary>
        [TestMethod]
        public void Category_NotSerialized_RestoredByConstructor()
        {
            var ops = new List<OperationBase>
            {
                new DrillPointsOperation(),
                new ProfileCircleOperation(),
                new PocketCircleOperation()
            };

            var json = Service.Serialize(ops);
            Assert.IsFalse(json.Contains("\"Category\"", StringComparison.Ordinal),
                "Category не должна сериализоваться в .ygc");

            var loaded = Service.Deserialize(json);
            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual(OperationCategory.Drill, loaded[0].Category);
            Assert.AreEqual(OperationCategory.Profile, loaded[1].Category);
            Assert.AreEqual(OperationCategory.Pocket, loaded[2].Category);
        }
    }
}
