using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Операция сообщает об изменении каждого своего параметра.
    ///
    /// На этих уведомлениях держатся перерисовка предпросмотра, признак
    /// несохранённого проекта и сброс уже сгенерированной программы. Раньше
    /// уведомляли только имя и признак «включена», а геометрия была обычными
    /// свойствами: каждое место, меняющее операцию, обязано было вручную
    /// позвать «содержимое изменилось», и забытый вызов проявлялся не ошибкой,
    /// а неперерисованным предпросмотром.
    ///
    /// Проверяются все параметры всех типов операций сразу: новый параметр,
    /// объявленный обычным свойством, немедленно роняет этот тест.
    /// </summary>
    [TestClass]
    public class OperationChangeNotificationTests
    {
        [TestMethod]
        public void EveryOperationParameter_RaisesChangeNotification()
        {
            var problems = new List<string>();
            var checkedParameters = 0;

            foreach (var descriptor in OperationCatalog.All)
            {
                var operation = descriptor.Create();
                foreach (var property in ChangeableProperties(operation.GetType()))
                {
                    checkedParameters++;
                    var raised = new List<string>();
                    PropertyChangedEventHandler handler = (_, e) => raised.Add(e.PropertyName);
                    operation.PropertyChanged += handler;
                    try
                    {
                        property.SetValue(operation, DifferentValue(property.GetValue(operation), property.PropertyType));
                    }
                    finally
                    {
                        operation.PropertyChanged -= handler;
                    }

                    if (!raised.Contains(property.Name))
                    {
                        problems.Add($"{operation.GetType().Name}.{property.Name}: изменение прошло молча"
                            + (raised.Count > 0 ? $" (сообщено о {string.Join(", ", raised)})" : string.Empty));
                    }
                }
            }

            Assert.IsTrue(checkedParameters > 100, $"Проверено параметров: {checkedParameters}");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// Повторная запись того же значения молчит: иначе предпросмотр
        /// пересобирался бы на каждое открытие диалога, а проект помечался
        /// изменённым без единой правки.
        /// </summary>
        [TestMethod]
        public void WritingSameValue_RaisesNothing()
        {
            foreach (var descriptor in OperationCatalog.All)
            {
                var operation = descriptor.Create();
                var raised = new List<string>();
                operation.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

                foreach (var property in ChangeableProperties(operation.GetType()))
                    property.SetValue(operation, property.GetValue(operation));

                Assert.AreEqual(0, raised.Count,
                    $"{operation.GetType().Name}: сообщено об изменении без изменения — {string.Join(", ", raised)}");
            }
        }

        /// <summary>
        /// Отверстие сверления правится прямо в таблице, поэтому оно тоже
        /// обязано сообщать об изменениях.
        /// </summary>
        [TestMethod]
        public void DrillHole_RaisesChangeNotification()
        {
            var hole = new DrillHole();
            var raised = new List<string>();
            hole.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            hole.X = 5;
            hole.TotalDepth = 3;

            CollectionAssert.AreEquivalent(new[] { nameof(DrillHole.X), nameof(DrillHole.TotalDepth) }, raised);
        }

        /// <summary>Параметры, которые пользователь может изменить.</summary>
        private static IEnumerable<PropertyInfo> ChangeableProperties(Type operationType)
            => operationType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.SetMethod?.IsPublic == true)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
                .OrderBy(p => p.Name, StringComparer.Ordinal);

        /// <summary>Значение, заведомо отличающееся от текущего.</summary>
        private static object DifferentValue(object current, Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(double))
                return (double)(current ?? 0.0) + 1.5;
            if (underlying == typeof(int))
                return (int)(current ?? 0) + 1;
            if (underlying == typeof(bool))
                return !(bool)(current ?? false);
            if (underlying == typeof(string))
                return (string)current == "изменено" ? "иначе" : "изменено";
            if (underlying.IsEnum)
            {
                var values = Enum.GetValues(underlying).Cast<object>().ToList();
                var index = values.FindIndex(v => Equals(v, current));
                return values[(index + 1) % values.Count];
            }
            if (typeof(IList).IsAssignableFrom(underlying))
                return Activator.CreateInstance(underlying);

            throw new NotSupportedException($"Не задано отличающееся значение для типа {underlying.Name}");
        }
    }
}
