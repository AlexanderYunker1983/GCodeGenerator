using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Копирование операций. Раньше поля перечислялись вручную в каждом месте,
    /// где нужна копия, и забытое при добавлении поле обнаруживалось только
    /// по неверному G-коду; здесь полнота копирования проверяется для каждого
    /// типа операции сразу.
    /// </summary>
    [TestClass]
    public class OperationClonerTests
    {
        /// <summary>Свойства, которые участвуют в сравнении копии с оригиналом.</summary>
        private static IEnumerable<PropertyInfo> ComparableProperties(Type type)
            => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead
                    && p.GetIndexParameters().Length == 0
                    && p.GetCustomAttribute<JsonIgnoreAttribute>() == null);

        private static void AssertValueEqual(object expected, object actual, string path)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected, actual, $"Значение {path}");
                return;
            }

            if (expected is string || expected.GetType().IsValueType)
            {
                Assert.AreEqual(expected, actual, $"Значение {path}");
                return;
            }

            if (expected is IEnumerable expectedItems && actual is IEnumerable actualItems)
            {
                var expectedList = expectedItems.Cast<object>().ToList();
                var actualList = actualItems.Cast<object>().ToList();
                Assert.AreEqual(expectedList.Count, actualList.Count, $"Число элементов {path}");
                for (int i = 0; i < expectedList.Count; i++)
                    AssertObjectEqual(expectedList[i], actualList[i], $"{path}[{i}]");
                return;
            }

            AssertObjectEqual(expected, actual, path);
        }

        private static void AssertObjectEqual(object expected, object actual, string path)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected, actual, $"Значение {path}");
                return;
            }

            Assert.AreEqual(expected.GetType(), actual.GetType(), $"Тип {path}");
            foreach (var property in ComparableProperties(expected.GetType()))
                AssertValueEqual(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}");
        }

        /// <summary>
        /// Эталонный набор покрывает все 11 типов операций со всеми режимами:
        /// копия каждой обязана совпадать с оригиналом по всем сохраняемым
        /// свойствам, включая вложенные списки отверстий и контуров.
        /// </summary>
        [TestMethod]
        public void Clone_EveryOperationType_CopiesAllPersistedProperties()
        {
            var operations = ReferenceOperations.Build();
            Assert.IsTrue(operations.Count > 0, "Эталонный набор операций не пуст");

            foreach (var operation in operations)
            {
                var clone = OperationCloner.Clone(operation);

                Assert.AreEqual(operation.GetType(), clone.GetType(), "Тип копии");
                Assert.AreNotSame(operation, clone, "Копия — другой объект");
                AssertObjectEqual(operation, clone, operation.GetType().Name);
            }
        }

        /// <summary>
        /// Свойства, восстанавливаемые конструктором, у копии на месте:
        /// категория операции не сохраняется в файл проекта и не копируется
        /// напрямую.
        /// </summary>
        [TestMethod]
        public void Clone_RestoresConstructorProvidedProperties()
        {
            var operation = new PocketCircleOperation();

            var clone = (PocketCircleOperation)OperationCloner.Clone(operation);

            Assert.AreEqual(OperationCategory.Pocket, clone.Category);
            Assert.AreEqual(OperationType.PocketMilling, clone.Type);
        }

        /// <summary>
        /// Список отверстий копируется целиком: изменение копии не должно
        /// затрагивать исходную операцию.
        /// </summary>
        [TestMethod]
        public void Clone_DrillHoles_AreIndependent()
        {
            var operation = new DrillPointsOperation
            {
                Holes =
                {
                    new DrillHole { X = 1, Y = 2, Z = 0, TotalDepth = 3, StepDepth = 1 }
                }
            };

            var clone = (DrillPointsOperation)OperationCloner.Clone(operation);
            clone.Holes[0].X = 100;
            clone.Holes.Add(new DrillHole { X = 5, Y = 5, TotalDepth = 1, StepDepth = 1 });

            Assert.AreEqual(1.0, operation.Holes[0].X, "Оригинал не меняется");
            Assert.AreEqual(1, operation.Holes.Count, "В оригинале по-прежнему одно отверстие");
        }

        /// <summary>
        /// Замкнутые контуры DXF-кармана копируются вглубь: черновой проход
        /// работает с копией операции и не должен портить импортированную
        /// геометрию.
        /// </summary>
        [TestMethod]
        public void Clone_DxfContours_AreIndependent()
        {
            var operation = new PocketDxfOperation
            {
                ClosedContours = new List<DxfPolyline>
                {
                    new DxfPolyline
                    {
                        Points = new List<DxfPoint>
                        {
                            new DxfPoint { X = 0, Y = 0 },
                            new DxfPoint { X = 10, Y = 0 },
                            new DxfPoint { X = 10, Y = 10 }
                        }
                    }
                }
            };

            var clone = (PocketDxfOperation)OperationCloner.Clone(operation);
            clone.ClosedContours[0].Points[0].X = 99;

            Assert.AreEqual(0.0, operation.ClosedContours[0].Points[0].X, "Контур оригинала не меняется");
        }

        [TestMethod]
        public void Clone_Null_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => OperationCloner.Clone((OperationBase)null));
        }

        /// <summary>
        /// Обобщённая перегрузка сохраняет конкретный тип операции: генератор
        /// кармана работает с интерфейсом, но копия должна остаться, например,
        /// операцией эллиптического кармана.
        /// </summary>
        [TestMethod]
        public void Clone_Generic_KeepsConcreteType()
        {
            GCodeGenerator.GCodeGenerators.Interfaces.IPocketOperation operation =
                new PocketEllipseOperation { RadiusX = 12, RadiusY = 8 };

            var clone = OperationCloner.Clone(operation);

            Assert.IsInstanceOfType(clone, typeof(PocketEllipseOperation));
            Assert.AreEqual(12.0, ((PocketEllipseOperation)clone).RadiusX);
            Assert.AreEqual(8.0, ((PocketEllipseOperation)clone).RadiusY);
        }
    }
}
