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
    /// Открытие диалога не должно менять параметры операции.
    ///
    /// Окно правит операцию напрямую, поэтому переносить значения туда и
    /// обратно больше не нужно — прежняя проверка круга «операция → диалог →
    /// OK → операция» ушла вместе с этим переносом. Но осталась другая
    /// опасность: диалог что-то делает при открытии — задаёт режим шаблона,
    /// создаёт первое отверстие, выбирает корпус, — и заполненная операция
    /// может незаметно потерять значение просто оттого, что её открыли
    /// и подтвердили.
    ///
    /// Проверяются все диалоги на готовых операциях эталонного набора.
    /// </summary>
    [TestClass]
    public class OperationEditorRoundTripTests
    {
        /// <summary>
        /// Параметры, которые диалог задаёт сам: режим шаблона он определяет
        /// своим типом, а список отверстий рассчитывает по шаблону.
        /// </summary>
        private static readonly HashSet<string> DialogOwned = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(DrillPointsOperation.DrillMode)
        };

        [TestMethod]
        public void OpeningAndAcceptingDialog_KeepsOperationParameters()
        {
            var problems = new List<string>();
            var checkedDialogs = 0;

            foreach (var editorType in EditorTypes())
            {
                var operation = OperationCloner.Clone(FindFixture(editorType));
                var before = Snapshot(operation);

                var editor = (IOperationEditorViewModel)CreateEditor(editorType);
                editor.SetOperation(operation);
                Ok(editor);
                checkedDialogs++;

                if (!editor.IsAccepted)
                {
                    problems.Add($"{editorType.Name}: диалог счёл готовую операцию невалидной");
                    continue;
                }

                foreach (var pair in before)
                {
                    if (DialogOwned.Contains(pair.Key))
                        continue;

                    var after = pair.Value.Property.GetValue(operation);
                    if (!Equals(after, pair.Value.Value))
                        problems.Add($"{editorType.Name}.{pair.Key}: было {pair.Value.Value}, стало {after}");
                }
            }

            Assert.IsTrue(checkedDialogs > 0, "Ни одного диалога редактора операции не найдено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// Диалог сверления по шаблону пересчитывает отверстия при открытии:
        /// список в операции обязан соответствовать её параметрам, даже если
        /// в файле проекта он от них отстал.
        /// </summary>
        [TestMethod]
        public void DrillPatternDialog_RecalculatesHolesOnOpen()
        {
            var operation = new DrillPointsOperation
            {
                DrillMode = DrillMode.Line,
                StartX = 0,
                StartY = 0,
                Distance = 10,
                HoleCount = 4,
                TotalDepth = 2,
                StepDepth = 1
            };
            operation.Holes.Clear(); // список отстал от параметров

            var editor = (IOperationEditorViewModel)CreateEditor(
                typeof(GCodeGenerator.ViewModels.Drill.DrillLineOperationViewModel));
            editor.SetOperation(operation);
            Ok(editor);

            Assert.IsTrue(editor.IsAccepted, "Заполненный шаблон принимается");
            Assert.AreEqual(4, operation.Holes.Count, "Отверстия пересчитаны по параметрам шаблона");
        }

        /// <summary>Значения параметров операции до открытия диалога.</summary>
        private static Dictionary<string, (PropertyInfo Property, object Value)> Snapshot(OperationBase operation)
            => operation.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.SetMethod?.IsPublic == true)
                .Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
                .ToDictionary(p => p.Name, p => (p, p.GetValue(operation)), StringComparer.Ordinal);

        private static void Ok(object editor)
            => ((ICommand)editor.GetType().GetProperty("OkCommand").GetValue(editor)).Execute(null);

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

            // Службы живут и в приложении, и в ядре: импорт чертежей,
            // например, переехал в ядро вместе с чтением DXF.
            var assemblies = new[] { typeof(MainViewModel).Assembly, typeof(OperationBase).Assembly };
            var implementation = assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(t => !t.IsAbstract
                                     && serviceType.IsAssignableFrom(t)
                                     && t.GetConstructor(Type.EmptyTypes) != null);

            return implementation == null ? null : Activator.CreateInstance(implementation);
        }
    }
}
