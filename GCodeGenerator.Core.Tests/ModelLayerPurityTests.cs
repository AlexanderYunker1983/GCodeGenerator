using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Чистота слоя моделей: домен не знает о слое генераторов. Направление
    /// зависимости — генераторы читают модели, а не наоборот; прежде базовые
    /// классы операций реализовывали интерфейсы из пространства генераторов,
    /// дублировавшие их же свойства, и новое общее свойство фрезеровки
    /// приходилось объявлять в трёх местах. Проверка держит границу
    /// рефлексией по собранной сборке — переименование пространств или
    /// новый тип её не обойдут.
    /// </summary>
    [TestClass]
    public class ModelLayerPurityTests
    {
        [TestMethod]
        public void ModelTypes_DoNotReferenceGeneratorLayer()
        {
            var assembly = typeof(OperationBase).Assembly;
            var offenders = new List<string>();

            var modelTypes = assembly.GetTypes()
                .Where(t => t.Namespace == "GCodeGenerator.Models")
                .ToList();
            Assert.IsTrue(modelTypes.Count > 20,
                $"в снимке слоя моделей {modelTypes.Count} типов — проверка ничего не проверяет");

            foreach (var type in modelTypes)
            {
                foreach (var referenced in ReferencedTypes(type))
                {
                    if (referenced?.Namespace != null
                        && referenced.Namespace.StartsWith("GCodeGenerator.GCodeGenerators", StringComparison.Ordinal))
                    {
                        offenders.Add($"{type.Name} -> {referenced.Name}");
                    }
                }
            }

            Assert.AreEqual(0, offenders.Distinct().Count(),
                "модели ссылаются на слой генераторов: " + string.Join("; ", offenders.Distinct()));
        }

        /// <summary>
        /// Типы, видимые в контракте типа: базовый класс, интерфейсы
        /// и сигнатуры открытых членов.
        /// </summary>
        private static IEnumerable<Type> ReferencedTypes(Type type)
        {
            if (type.BaseType != null)
                yield return type.BaseType;

            foreach (var contract in type.GetInterfaces())
                yield return contract;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var property in type.GetProperties(flags))
                yield return property.PropertyType;

            foreach (var field in type.GetFields(flags))
                yield return field.FieldType;

            foreach (var method in type.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (var parameter in method.GetParameters())
                    yield return parameter.ParameterType;
            }

            foreach (var constructor in type.GetConstructors(flags))
            {
                foreach (var parameter in constructor.GetParameters())
                    yield return parameter.ParameterType;
            }
        }
    }
}
