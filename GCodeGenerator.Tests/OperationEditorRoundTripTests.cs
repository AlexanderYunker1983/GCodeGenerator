using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Перенос параметров между операцией и диалогом.
    ///
    /// Диалог читает значения операции в свои свойства и по OK записывает их
    /// обратно. Если очередной параметр забыли в одном из этих двух методов,
    /// сборка не ломается: поле в окне живёт своей жизнью, а введённое
    /// значение либо теряется при закрытии, либо подменяется значением по
    /// умолчанию при открытии. Именно так безопасное расстояние между
    /// проходами не доезжало до операции по чертежу.
    ///
    /// Тест сверяет пары «свойство операции — одноимённое свойство диалога»:
    /// раз оба существуют, значение обязано пережить круг
    /// операция → диалог → OK → операция.
    ///
    /// За круг меняется ровно один параметр: часть из них связана друг с
    /// другом (черновая и чистовая обработка взаимоисключающие), и групповая
    /// правка сама нарушала бы эти зависимости.
    /// </summary>
    [TestClass]
    public class OperationEditorRoundTripTests
    {
        [TestMethod]
        public void EveryEditor_CarriesMatchingValuesBothWays()
        {
            var problems = new List<string>();
            var checkedParameters = 0;
            var editorTypes = EditorTypes().ToList();

            foreach (var editorType in editorTypes)
            {
                var template = FindFixture(editorType);
                foreach (var pair in MatchingProperties(editorType, template.GetType()))
                {
                    checkedParameters++;
                    var operation = OperationCloner.Clone(template);
                    var stored = NextValue(pair.Operation.GetValue(operation));
                    pair.Operation.SetValue(operation, stored);

                    var editor = (IOperationEditorViewModel)CreateEditor(editorType);
                    editor.SetOperation(operation);

                    var shown = pair.Editor.GetValue(editor);
                    if (!Equals(shown, stored))
                    {
                        problems.Add($"{editorType.Name}.{pair.Operation.Name}: окно открылось со значением " +
                                     $"{shown} вместо сохранённого {stored}");
                        continue;
                    }

                    var entered = NextValue(stored);
                    pair.Editor.SetValue(editor, entered);
                    Ok(editor);

                    if (!editor.IsAccepted)
                    {
                        problems.Add($"{editorType.Name}.{pair.Operation.Name}: диалог счёл операцию невалидной");
                        continue;
                    }

                    var saved = pair.Operation.GetValue(operation);
                    if (!Equals(saved, entered))
                        problems.Add($"{editorType.Name}.{pair.Operation.Name}: введённое {entered} не сохранилось, " +
                                     $"в операции осталось {saved}");
                }
            }

            Assert.IsTrue(editorTypes.Count > 0, "Ни одного диалога редактора операции не найдено");
            Assert.IsTrue(checkedParameters > 0, "Ни одного параметра не проверено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>Все диалоги редактирования операций приложения.</summary>
        private static IEnumerable<Type> EditorTypes()
            => typeof(MainViewModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IOperationEditorViewModel).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        /// <summary>
        /// Готовая операция для диалога — из набора фикстур: она заполнена
        /// и содержит геометрию, без которой диалоги по чертежу отказываются
        /// сохранять. Сверление разбирается точнее: тип операции у всех
        /// шаблонов один, поэтому нужна фикстура того же шаблона, который
        /// редактирует диалог.
        /// </summary>
        private static OperationBase FindFixture(Type editorType)
        {
            var operationType = GetOperationType(editorType);
            var fixtures = ReferenceOperations.Build().Where(operationType.IsInstanceOfType);

            var mode = DrillModeOf(editorType);
            if (mode != null)
                fixtures = fixtures.Where(o => ((DrillPointsOperation)o).DrillMode == mode.Value);

            var fixture = fixtures.FirstOrDefault();
            Assert.IsNotNull(fixture, $"Нет фикстуры для {editorType.Name}");
            return fixture;
        }

        /// <summary>Шаблон сверления диалога или пусто, если диалог не о сверлении.</summary>
        private static DrillMode? DrillModeOf(Type editorType)
        {
            var mode = editorType.GetProperty("Mode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mode == null || mode.PropertyType != typeof(DrillMode))
                return null;

            return (DrillMode)mode.GetValue(CreateEditor(editorType));
        }

        /// <summary>Тип операции, которую редактирует диалог.</summary>
        private static Type GetOperationType(Type editorType)
        {
            for (var type = editorType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OperationEditorViewModelBase<>))
                    return type.GetGenericArguments()[0];
            }

            throw new InvalidOperationException($"{editorType.Name} не наследует OperationEditorViewModelBase<>");
        }

        /// <summary>
        /// Создаёт диалог. Окно здесь не показывается, но конструктор может
        /// требовать сервис (импорт чертежа), поэтому для каждой зависимости
        /// берётся готовая реализация приложения без параметров, а если такой
        /// нет — пустая ссылка: локализация и выбор файла тесту не нужны.
        /// </summary>
        private static object CreateEditor(Type editorType)
        {
            var constructor = editorType.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var arguments = constructor.GetParameters()
                .Select(p => CreateDependency(p.ParameterType))
                .ToArray();

            return constructor.Invoke(arguments);
        }

        private static object CreateDependency(Type serviceType)
        {
            if (!serviceType.IsInterface && !serviceType.IsAbstract)
                return Activator.CreateInstance(serviceType);

            var implementation = typeof(MainViewModel).Assembly.GetTypes()
                .FirstOrDefault(t => !t.IsAbstract
                                     && serviceType.IsAssignableFrom(t)
                                     && t.GetConstructor(Type.EmptyTypes) != null);

            return implementation == null ? null : Activator.CreateInstance(implementation);
        }

        /// <summary>Одноимённые свойства операции и диалога.</summary>
        private sealed class ParameterPair
        {
            public ParameterPair(PropertyInfo operation, PropertyInfo editor)
            {
                Operation = operation;
                Editor = editor;
            }

            public PropertyInfo Operation { get; }

            public PropertyInfo Editor { get; }
        }

        /// <summary>
        /// Свойства операции, у которых есть одноимённое свойство диалога того
        /// же типа. Списки точек и имена файлов не сверяются: их диалог правит
        /// собственными командами, а не текстовым полем.
        /// </summary>
        private static IEnumerable<ParameterPair> MatchingProperties(Type editorType, Type operationType)
        {
            var editorProperties = editorType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

            return operationType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType))
                .Select(p => editorProperties.TryGetValue(p.Name, out var editorProperty)
                             && editorProperty.PropertyType == p.PropertyType
                    ? new ParameterPair(p, editorProperty)
                    : null)
                .Where(pair => pair != null)
                .OrderBy(pair => pair.Operation.Name, StringComparer.Ordinal);
        }

        private static bool IsScalar(Type type)
            => type.IsEnum || type == typeof(double) || type == typeof(int) || type == typeof(bool);

        /// <summary>
        /// Значение, заведомо отличное от текущего, но остающееся осмысленным:
        /// размеры и подачи растут в полтора раза с небольшим сдвигом (нулевые
        /// параметры тоже становятся ненулевыми), счётчики увеличиваются на
        /// единицу, флаги переключаются, перечисления идут к следующему
        /// варианту. Заведомо невалидное значение диалог отверг бы вместо
        /// сохранения.
        /// </summary>
        private static object NextValue(object current)
        {
            switch (current)
            {
                case double value:
                    return value * 1.25 + 0.5;
                case int value:
                    return value + 1;
                case bool value:
                    return !value;
                case Enum value:
                    var options = Enum.GetValues(value.GetType());
                    var index = Array.IndexOf(options, value);
                    return options.GetValue((index + 1) % options.Length);
                default:
                    throw new InvalidOperationException($"Неизвестный тип значения: {current?.GetType()}");
            }
        }

        private static void Ok(IOperationEditorViewModel editor)
            => ((ICommand)editor.GetType().GetProperty("OkCommand").GetValue(editor)).Execute(null);
    }
}
