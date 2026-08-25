using System;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Каталог типов операций и его покрытие остальными механизмами.
    ///
    /// Сведения о типе операции нужны файлу проекта, генератору G-кода,
    /// фабрике геометрии, диалогу редактора и построителю превью. Раньше
    /// каждый из них перечислял типы самостоятельно, и пропуск обнаруживался
    /// в разное время — от отказа сохранить проект до молчаливого отсутствия
    /// операции в превью. Эти тесты требуют, чтобы каждый тип из каталога был
    /// известен всем механизмам, а каталог, в свою очередь, содержал все типы
    /// операций продукта.
    /// </summary>
    [TestClass]
    public class OperationCatalogTests
    {
        /// <summary>
        /// Каталог обязан содержать каждый тип операции, объявленный в ядре:
        /// новый тип, забытый в каталоге, не сохранится в проект и не получит
        /// генератора.
        /// </summary>
        [TestMethod]
        public void Catalog_ContainsEveryOperationTypeOfCore()
        {
            var declaredTypes = typeof(OperationBase).Assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && typeof(OperationBase).IsAssignableFrom(type))
                .OrderBy(type => type.Name)
                .ToList();

            Assert.IsTrue(declaredTypes.Count > 0, "В ядре должны быть типы операций");

            foreach (var type in declaredTypes)
            {
                Assert.IsNotNull(
                    OperationCatalog.FindByType(type),
                    $"Тип операции {type.Name} отсутствует в каталоге");
            }

            Assert.AreEqual(declaredTypes.Count, OperationCatalog.All.Count,
                "В каталоге не должно быть записей без соответствующего типа операции");
        }

        /// <summary>Имена в файле проекта уникальны: иначе тип нельзя восстановить однозначно.</summary>
        [TestMethod]
        public void Catalog_PersistentNames_AreUnique()
        {
            var names = OperationCatalog.All.Select(d => d.PersistentName).ToList();

            CollectionAssert.AllItemsAreUnique(names);
            foreach (var name in names)
                Assert.IsFalse(string.IsNullOrWhiteSpace(name));
        }

        /// <summary>
        /// Фабрика каталога создаёт операцию заявленного типа и категории:
        /// категория определяет и вкладку в интерфейсе, и выбор генератора.
        /// </summary>
        [TestMethod]
        public void Catalog_Create_ProducesDeclaredTypeAndCategory()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var operation = descriptor.Create();

                Assert.IsNotNull(operation, $"{descriptor.PersistentName}: фабрика вернула null");
                Assert.AreEqual(descriptor.OperationType, operation.GetType(), descriptor.PersistentName);
                Assert.AreEqual(descriptor.Category, operation.Category, descriptor.PersistentName);
            }
        }

        /// <summary>Имя типа из каталога и имя класса читаются одинаково.</summary>
        [TestMethod]
        public void Catalog_ResolvesBothPersistentAndClassNames()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                Assert.AreSame(descriptor, OperationCatalog.FindByPersistentName(descriptor.PersistentName));
                Assert.AreSame(descriptor, OperationCatalog.FindByPersistentName(descriptor.OperationType.Name));
                Assert.AreSame(descriptor, OperationCatalog.FindByPersistentName(
                    descriptor.PersistentName.ToUpperInvariant()));
            }

            Assert.IsNull(OperationCatalog.FindByPersistentName("НеизвестныйТип"));
            Assert.IsNull(OperationCatalog.FindByPersistentName(null));
        }

        [TestMethod]
        public void Catalog_UnknownType_Throws()
        {
            Assert.ThrowsException<NotSupportedException>(() => OperationCatalog.ForType(typeof(string)));
            Assert.ThrowsException<ArgumentNullException>(() => OperationCatalog.ForType(null));
        }

        /// <summary>Для каждого типа каталога зарегистрирован генератор G-кода.</summary>
        [TestMethod]
        public void EveryCatalogType_HasGenerator()
        {
            var registry = new OperationGeneratorRegistry();

            foreach (var descriptor in OperationCatalog.All)
            {
                Assert.IsTrue(
                    registry.TryGetGenerator(descriptor.OperationType, out var generator),
                    $"{descriptor.PersistentName}: нет генератора");
                Assert.IsNotNull(generator, descriptor.PersistentName);
            }
        }

        /// <summary>
        /// Для каждого профиля и кармана строится геометрия: без неё операция
        /// не даст ни траектории, ни превью.
        /// </summary>
        [TestMethod]
        public void EveryProfileAndPocketType_HasGeometry()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var operation = descriptor.Create();
                switch (descriptor.Category)
                {
                    case OperationCategory.Profile:
                        Assert.IsInstanceOfType(operation, typeof(IProfileOperation), descriptor.PersistentName);
                        Assert.IsNotNull(ProfileGeometryFactory.Create(operation), descriptor.PersistentName);
                        break;
                    case OperationCategory.Pocket:
                        Assert.IsInstanceOfType(operation, typeof(IPocketOperation), descriptor.PersistentName);
                        Assert.IsNotNull(PocketGeometryFactory.Create(operation), descriptor.PersistentName);
                        break;
                }
            }
        }

        /// <summary>
        /// Имя типа записывается в файл проекта и читается обратно для каждого
        /// типа каталога.
        /// </summary>
        [TestMethod]
        public void EveryCatalogType_RoundTripsThroughProjectFileNames()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var name = OperationTypeNames.ToShortName(descriptor.OperationType);

                Assert.AreEqual(descriptor.PersistentName, name);
                Assert.AreEqual(descriptor.OperationType, OperationTypeNames.Resolve(name));
            }
        }

        /// <summary>
        /// Построитель превью распознаёт каждый тип каталога: нераспознанная
        /// операция просто не появилась бы на схеме, без всякого сообщения.
        /// </summary>
        [TestMethod]
        public void EveryCatalogType_IsRecognizedByScenePreview()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var operation = descriptor.Create();
                PrepareForPreview(operation);

                var scene = OperationSceneBuilder.Build(new[] { operation });

                Assert.IsTrue(scene.Shapes.Count > 0,
                    $"{descriptor.PersistentName}: операция не попала в превью");
            }
        }

        /// <summary>
        /// Операции, геометрия которых приходит из файла или диалога, создаются
        /// пустыми: чтобы проверить распознавание типа, им нужен минимальный
        /// набор точек.
        /// </summary>
        private static void PrepareForPreview(OperationBase operation)
        {
            switch (operation)
            {
                case DrillPointsOperation drill:
                    drill.Holes.Add(new DrillHole { X = 1, Y = 1, TotalDepth = 1, StepDepth = 1 });
                    break;
                case ProfileDxfOperation profileDxf:
                    profileDxf.Polylines.Add(Square());
                    break;
                case PocketDxfOperation pocketDxf:
                    pocketDxf.ClosedContours.Add(Square());
                    break;
            }
        }

        private static DxfPolyline Square()
            => new DxfPolyline
            {
                Points =
                {
                    new DxfPoint { X = 0, Y = 0 },
                    new DxfPoint { X = 10, Y = 0 },
                    new DxfPoint { X = 10, Y = 10 },
                    new DxfPoint { X = 0, Y = 10 },
                    new DxfPoint { X = 0, Y = 0 }
                }
            };
    }
}
