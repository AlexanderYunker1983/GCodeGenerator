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
            Assert.IsTrue(Directory.Exists(ViewsDirectory), $"Нет каталога разметки: {ViewsDirectory}");

            var assembly = typeof(MainViewModel).Assembly;
            var problems = new List<string>();
            var checkedViews = 0;

            foreach (var xamlPath in Directory.GetFiles(ViewsDirectory, "*.xaml", SearchOption.AllDirectories))
            {
                var viewName = Path.GetFileNameWithoutExtension(xamlPath);
                if (!viewName.EndsWith("View", StringComparison.Ordinal))
                    continue;

                var viewModelName = viewName + "Model";
                var viewModelType = assembly.GetTypes().FirstOrDefault(t => t.Name == viewModelName);
                if (viewModelType == null)
                    continue;

                checkedViews++;
                var properties = CollectBindableNames(viewModelType);

                var xaml = File.ReadAllText(xamlPath);
                var paths = Regex.Matches(xaml, @"\{Binding\s+(?:Path=)?([A-Za-z_][A-Za-z0-9_]*)")
                    .Select(m => m.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal);

                foreach (var path in paths)
                {
                    if (BindingKeywords.Contains(path))
                        continue;
                    if (!properties.Contains(path))
                        problems.Add($"{Path.GetFileName(xamlPath)}: {{Binding {path}}} — нет свойства в {viewModelName}");
                }
            }

            Assert.IsTrue(checkedViews > 0, "Ни одного представления с view-моделью не найдено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

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
