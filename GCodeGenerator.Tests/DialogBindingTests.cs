using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Привязки диалогов к свойствам view-моделей.
    ///
    /// Опечатка или переименование свойства не ломает сборку: WPF просто
    /// не находит источник и оставляет поле пустым, а пользователь вводит
    /// значение, которое никуда не попадает. Поэтому соответствие проверяется
    /// здесь — по исходным файлам разметки.
    ///
    /// Разметка окна собирается вместе с общими блоками, которые оно
    /// подключает: блок наследует источник данных окна, поэтому его привязки
    /// адресуют ту же view-модель.
    /// </summary>
    [TestClass]
    public class DialogBindingTests
    {
        /// <summary>Каталог разметки приложения относительно каталога сборки тестов.</summary>
        private static string ViewsDirectory =>
            Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "GCodeGenerator", "Views"));

        /// <summary>
        /// Служебные имена внутри выражения привязки: относятся к самой
        /// привязке, а не к свойству источника.
        /// </summary>
        private static readonly HashSet<string> BindingKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "RelativeSource", "ElementName", "Source", "Mode", "Converter", "ConverterParameter",
            "StringFormat", "UpdateSourceTrigger", "Path", "FallbackValue", "TargetNullValue"
        };

        [TestMethod]
        public void EveryDialogBinding_ResolvesToViewModelProperty()
        {
            var problems = new List<string>();
            var checkedViews = 0;

            foreach (var (viewPath, viewModelType) in Dialogs())
            {
                checkedViews++;
                var properties = CollectBindableNames(viewModelType);

                foreach (var path in BoundPaths(viewPath))
                {
                    if (BindingKeywords.Contains(path))
                        continue;
                    if (!properties.Contains(path))
                        problems.Add($"{Path.GetFileName(viewPath)}: {{Binding {path}}} — нет свойства в {viewModelType.Name}");
                }
            }

            Assert.IsTrue(checkedViews > 0, "Ни одного представления с view-моделью не найдено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// Параметры, которые окно намеренно не показывает: операция их
        /// хранит и переносит, но на её G-code они не влияют.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> HiddenParameters =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                // Прямоугольник и многоугольник состоят из прямых: дуги не
                // аппроксимируются, длина отрезка ни на что не влияет.
                ["ProfileRectangleOperationView.xaml"] = new HashSet<string>(StringComparer.Ordinal) { "MaxSegmentLength" },
                ["ProfilePolygonOperationView.xaml"] = new HashSet<string>(StringComparer.Ordinal) { "MaxSegmentLength" },

                // Между отверстиями инструмент идёт только на быстрой подаче,
                // рабочая подача в плоскости при сверлении не используется.
                ["DrillLineOperationView.xaml"] = DrillWorkFeed,
                ["DrillArrayOperationView.xaml"] = DrillWorkFeed,
                ["DrillRectOperationView.xaml"] = DrillWorkFeed,
                ["DrillCircleOperationView.xaml"] = DrillWorkFeed,
                ["DrillArcOperationView.xaml"] = DrillWorkFeed,
                ["DrillPolygonOperationView.xaml"] = DrillWorkFeed,
                ["DrillEllipseOperationView.xaml"] = DrillWorkFeed,
                ["DrillPackageOperationView.xaml"] = DrillWorkFeed
            };

        private static HashSet<string> DrillWorkFeed
            => new HashSet<string>(StringComparer.Ordinal) { "FeedXYWork" };

        /// <summary>
        /// Обратная проверка: параметр операции, который диалог умеет читать и
        /// сохранять, обязан быть на виду. Иначе значение существует, влияет на
        /// G-code, но изменить его в окне нельзя — так у карманов по окружности
        /// и эллипсу пропала высота отвода, а у контура из чертежа — сторона
        /// обхода. Исключения перечислены явно и обоснованы.
        /// </summary>
        [TestMethod]
        public void EveryEditableParameter_IsShownInDialog()
        {
            var problems = new List<string>();
            var checkedParameters = 0;

            foreach (var (viewPath, viewModelType) in Dialogs())
            {
                var operationType = OperationTypeOf(viewModelType);
                if (operationType == null)
                    continue;

                var viewName = Path.GetFileName(viewPath);
                var shown = new HashSet<string>(BoundPaths(viewPath), StringComparer.Ordinal);
                HiddenParameters.TryGetValue(viewName, out var hidden);

                foreach (var parameter in EditableParameters(viewModelType, operationType))
                {
                    if (hidden != null && hidden.Contains(parameter))
                        continue;

                    checkedParameters++;
                    if (!shown.Contains(parameter))
                        problems.Add($"{viewName}: {parameter} правится диалогом, но не показан");
                }
            }

            Assert.IsTrue(checkedParameters > 0, "Ни одного параметра не проверено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>Окна с view-моделью по конвенции имён.</summary>
        private static IEnumerable<(string ViewPath, Type ViewModelType)> Dialogs()
        {
            Assert.IsTrue(Directory.Exists(ViewsDirectory), $"Нет каталога разметки: {ViewsDirectory}");

            var assembly = typeof(MainViewModel).Assembly;
            foreach (var xamlPath in Directory.GetFiles(ViewsDirectory, "*.xaml", SearchOption.AllDirectories))
            {
                var viewName = Path.GetFileNameWithoutExtension(xamlPath);
                if (!viewName.EndsWith("View", StringComparison.Ordinal))
                    continue;

                var viewModelType = assembly.GetTypes().FirstOrDefault(t => t.Name == viewName + "Model");
                if (viewModelType != null)
                    yield return (xamlPath, viewModelType);
            }
        }

        /// <summary>
        /// Пути привязок окна вместе с путями общих блоков, которые оно
        /// подключает.
        /// </summary>
        private static IEnumerable<string> BoundPaths(string xamlPath)
        {
            var files = new List<string> { xamlPath };
            files.AddRange(IncludedBlocks(xamlPath));

            return files
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_]*)")
                    .Select(m => m.Groups[1].Value))
                .Distinct(StringComparer.Ordinal);
        }

        /// <summary>Файлы общих блоков, подключённых окном.</summary>
        private static IEnumerable<string> IncludedBlocks(string xamlPath)
        {
            var commonDirectory = Path.Combine(ViewsDirectory, "Common");

            return Regex.Matches(File.ReadAllText(xamlPath), @"<common:([A-Za-z0-9_]+)")
                .Select(m => Path.Combine(commonDirectory, m.Groups[1].Value + ".xaml"))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Тип операции, которую редактирует диалог, или <c>null</c>.</summary>
        private static Type OperationTypeOf(Type viewModelType)
        {
            for (var type = viewModelType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(OperationEditorViewModelBase<>))
                    return type.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// Параметры, которые диалог переносит в операцию: одноимённые
        /// свойства простых типов у view-модели и у операции.
        /// </summary>
        private static IEnumerable<string> EditableParameters(Type viewModelType, Type operationType)
        {
            var operationProperties = operationType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite && IsScalar(p.PropertyType))
                .ToDictionary(p => p.Name, p => p.PropertyType, StringComparer.Ordinal);

            return viewModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => operationProperties.TryGetValue(p.Name, out var operationType2)
                            && operationType2 == p.PropertyType)
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.Ordinal);
        }

        private static bool IsScalar(Type type)
            => type.IsEnum || type == typeof(double) || type == typeof(int) || type == typeof(bool);

        /// <summary>
        /// Имена, к которым может обращаться разметка окна: собственные
        /// свойства view-модели и свойства элементов её коллекций — внутри
        /// шаблона списка источником становится сам элемент (отверстие,
        /// корпус, операция).
        /// </summary>
        private static HashSet<string> CollectBindableNames(Type viewModelType)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var properties = viewModelType.GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                names.Add(property.Name);

                var itemType = GetCollectionItemType(property.PropertyType);
                if (itemType == null)
                    continue;

                foreach (var itemProperty in itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    names.Add(itemProperty.Name);
            }

            return names;
        }

        /// <summary>Тип элемента коллекции или <c>null</c>, если свойство не коллекция.</summary>
        private static Type GetCollectionItemType(Type type)
        {
            if (type == typeof(string))
                return null;

            return type.GetInterfaces()
                .Concat(type.IsGenericType ? new[] { type } : Array.Empty<Type>())
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .Select(i => i.GetGenericArguments()[0])
                .FirstOrDefault();
        }
    }
}
