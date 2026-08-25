#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GCodeGenerator.Views
{
    /// <summary>
    /// Соответствие «view-модель → окно», построенное по типам сборки.
    ///
    /// Прежде окно искалось по собранной строке: пространство имён
    /// view-модели с заменой «ViewModels» на «Views», имя класса без
    /// суффикса плюс «View», затем поиск типа по этому имени. Пока имена
    /// совпадали, это работало; переименование пространства имён сборку не
    /// ломало — окно просто переставало находиться, и узнать об этом можно
    /// было, только открыв его.
    ///
    /// Теперь пары ищутся среди настоящих типов: окно <c>XxxView</c>
    /// связывается с view-моделью <c>XxxViewModel</c> той же сборки
    /// независимо от того, в каких пространствах имён они лежат.
    /// </summary>
    public static class DialogViewRegistry
    {
        private const string ViewSuffix = "View";
        private const string ViewModelSuffix = "ViewModel";

        private static readonly Dictionary<Type, Type> ViewsByViewModel = BuildRegistry();

        /// <summary>Все найденные пары «view-модель → окно».</summary>
        public static IReadOnlyDictionary<Type, Type> All => ViewsByViewModel;

        /// <summary>
        /// Тип окна для view-модели. Отсутствие пары — ошибка сборки
        /// приложения, а не пользовательский случай: показать view-модель
        /// без окна невозможно.
        /// </summary>
        /// <param name="viewModelType">Тип view-модели.</param>
        public static Type ViewFor(Type viewModelType)
        {
            if (viewModelType == null)
                throw new ArgumentNullException(nameof(viewModelType));

            if (ViewsByViewModel.TryGetValue(viewModelType, out var viewType))
                return viewType;

            throw new InvalidOperationException(
                $"Не найдено окно для view-модели {viewModelType.FullName} "
                + $"(ожидался класс {StripSuffix(viewModelType.Name, ViewModelSuffix)}{ViewSuffix}).");
        }

        /// <summary>
        /// Пары ищутся один раз: типы сборки за время работы не меняются.
        /// </summary>
        private static Dictionary<Type, Type> BuildRegistry()
        {
            var types = typeof(DialogViewRegistry).Assembly.GetTypes();

            var viewsByName = types
                .Where(type => !type.IsAbstract
                    && typeof(Window).IsAssignableFrom(type)
                    && type.Name.EndsWith(ViewSuffix, StringComparison.Ordinal))
                .ToDictionary(type => StripSuffix(type.Name, ViewSuffix), type => type, StringComparer.Ordinal);

            var registry = new Dictionary<Type, Type>();
            foreach (var viewModelType in types.Where(type => !type.IsAbstract
                && type.Name.EndsWith(ViewModelSuffix, StringComparison.Ordinal)))
            {
                var baseName = StripSuffix(viewModelType.Name, ViewModelSuffix);
                if (viewsByName.TryGetValue(baseName, out var viewType))
                    registry[viewModelType] = viewType;
            }

            return registry;
        }

        private static string StripSuffix(string name, string suffix)
            => name.EndsWith(suffix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
    }
}
