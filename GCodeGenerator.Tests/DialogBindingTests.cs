using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GCodeGenerator.Models;
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
                var shown = new HashSet<string>(
                    BoundPaths(viewPath).Select(StripOperationPrefix), StringComparer.Ordinal);
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
                .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_.]*)")
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
        /// Параметры операции, которые пользователь может изменить: простые
        /// свойства с сеттером. Раньше список строился по одноимённым
        /// свойствам диалога — теперь диалог их не заводит, и проверка идёт
        /// прямо от операции, то есть строже: параметр не спрячется от неё,
        /// даже если о нём забыли везде.
        /// </summary>
        private static IEnumerable<string> EditableParameters(Type viewModelType, Type operationType)
            => operationType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.SetMethod?.IsPublic == true && IsScalar(p.PropertyType))
                .Where(p => p.Name != nameof(OperationBase.IsEnabled) && p.Name != nameof(OperationBase.Name))
                .Where(p => !IsPatternParameterOfOtherMode(viewModelType, operationType, p.Name))
                .Select(p => p.Name)
                .OrderBy(name => name, StringComparer.Ordinal);

        /// <summary>
        /// Сверление описывает девять шаблонов одним типом операции, поэтому
        /// у каждого диалога свои параметры: окно линии не показывает радиус
        /// окружности и наоборот. Признак шаблона — параметр объявлен
        /// в операции, но не встречается ни в одном окне этого режима.
        /// </summary>
        private static bool IsPatternParameterOfOtherMode(Type viewModelType, Type operationType, string parameter)
        {
            if (operationType != typeof(DrillPointsOperation))
                return false;

            return !DrillParametersShownAnywhere(viewModelType).Contains(parameter);
        }

        /// <summary>Параметры, которые показывает именно это окно сверления.</summary>
        private static HashSet<string> DrillParametersShownAnywhere(Type viewModelType)
        {
            var viewPath = Dialogs().FirstOrDefault(d => d.ViewModelType == viewModelType).ViewPath;
            if (viewPath == null)
                return new HashSet<string>(StringComparer.Ordinal);

            return new HashSet<string>(
                BoundPaths(viewPath).Select(StripOperationPrefix),
                StringComparer.Ordinal);
        }

        private static string StripOperationPrefix(string path)
            => path.StartsWith("Operation.", StringComparison.Ordinal) ? path.Substring("Operation.".Length) : path;

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
            AddBindableNames(viewModelType, names, names, depth: 0);
            return names;
        }

        /// <summary>
        /// Имена, доступные разметке: свойства самого источника, свойства
        /// элементов его коллекций (внутри шаблона списка источником
        /// становится элемент) и пути через вложенные объекты — окно операции
        /// привязано прямо к её параметрам, то есть «Operation.StepDepth».
        /// </summary>
        private static void AddBindableNames(Type type, HashSet<string> names, HashSet<string> rowNames, int depth)
        {
            if (type == null || depth > 2)
                return;

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                names.Add(property.Name);

                var itemType = GetCollectionItemType(property.PropertyType);
                if (itemType != null)
                {
                    // Внутри шаблона списка источником становится сам элемент —
                    // отверстие, корпус, операция, — поэтому его свойства
                    // адресуются без пути к коллекции.
                    foreach (var itemProperty in itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                        rowNames.Add(itemProperty.Name);
                    continue;
                }

                if (IsScalar(property.PropertyType) || property.PropertyType == typeof(string))
                    continue;

                // Составной путь: «Operation.StepDepth» и подобные.
                var nested = new HashSet<string>(StringComparer.Ordinal);
                AddBindableNames(property.PropertyType, nested, rowNames, depth + 1);
                foreach (var name in nested)
                    names.Add($"{property.Name}.{name}");
            }
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
