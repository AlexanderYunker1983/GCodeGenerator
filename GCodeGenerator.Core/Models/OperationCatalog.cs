using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Описание одного типа операции: всё, что о нём нужно знать общим
    /// механизмам — файлу проекта, выбору генератора и построению превью.
    /// </summary>
    public sealed class OperationDescriptor
    {
        internal OperationDescriptor(
            Type operationType,
            string persistentName,
            OperationCategory category,
            Func<OperationBase> create)
        {
            OperationType = operationType;
            PersistentName = persistentName;
            Category = category;
            Create = create;
        }

        /// <summary>Тип модели операции.</summary>
        public Type OperationType { get; }

        /// <summary>
        /// Имя типа в файле проекта. Не совпадает с именем класса намеренно:
        /// переименование класса не должно ломать чтение сохранённых проектов.
        /// </summary>
        public string PersistentName { get; }

        /// <summary>Категория: сверление, профиль или карман.</summary>
        public OperationCategory Category { get; }

        /// <summary>Создаёт операцию этого типа со значениями по умолчанию.</summary>
        public Func<OperationBase> Create { get; }
    }

    /// <summary>
    /// Перечень типов операций продукта.
    ///
    /// Сведения о типе операции были рассыпаны по шести местам: короткое имя
    /// для файла проекта, соответствие генератору G-кода, фабрики геометрии
    /// профиля и кармана, тип диалога редактора и разбор типов при построении
    /// превью. Добавляя тип операции, легко было пропустить любое из них,
    /// и пропуск обнаруживался в разное время — от отказа сохранить проект
    /// до молчаливого отсутствия операции в превью.
    ///
    /// Каталог перечисляет типы один раз; остальные механизмы либо строятся
    /// от него, либо проверяются по нему тестами покрытия.
    /// </summary>
    public static class OperationCatalog
    {
        private static readonly OperationDescriptor[] Descriptors =
        {
            // Сверление: девять режимов — один тип операции (режим в DrillMode).
            new OperationDescriptor(typeof(DrillPointsOperation), "DrillPoints", OperationCategory.Drill,
                () => new DrillPointsOperation()),

            // Профили.
            new OperationDescriptor(typeof(ProfileRectangleOperation), "ProfileRectangle", OperationCategory.Profile,
                () => new ProfileRectangleOperation()),
            new OperationDescriptor(typeof(ProfileRoundedRectangleOperation), "ProfileRoundedRectangle", OperationCategory.Profile,
                () => new ProfileRoundedRectangleOperation()),
            new OperationDescriptor(typeof(ProfileCircleOperation), "ProfileCircle", OperationCategory.Profile,
                () => new ProfileCircleOperation()),
            new OperationDescriptor(typeof(ProfileEllipseOperation), "ProfileEllipse", OperationCategory.Profile,
                () => new ProfileEllipseOperation()),
            new OperationDescriptor(typeof(ProfilePolygonOperation), "ProfilePolygon", OperationCategory.Profile,
                () => new ProfilePolygonOperation()),
            new OperationDescriptor(typeof(ProfileDxfOperation), "ProfileDxf", OperationCategory.Profile,
                () => new ProfileDxfOperation()),

            // Карманы.
            new OperationDescriptor(typeof(PocketRectangleOperation), "PocketRectangle", OperationCategory.Pocket,
                () => new PocketRectangleOperation()),
            new OperationDescriptor(typeof(PocketCircleOperation), "PocketCircle", OperationCategory.Pocket,
                () => new PocketCircleOperation()),
            new OperationDescriptor(typeof(PocketEllipseOperation), "PocketEllipse", OperationCategory.Pocket,
                () => new PocketEllipseOperation()),
            new OperationDescriptor(typeof(PocketDxfOperation), "PocketDxf", OperationCategory.Pocket,
                () => new PocketDxfOperation()),
        };

        private static readonly Dictionary<Type, OperationDescriptor> ByOperationType =
            Descriptors.ToDictionary(descriptor => descriptor.OperationType);

        /// <summary>
        /// Имена типов для чтения файла проекта: имя из каталога и имя класса.
        /// Второе поддерживает файлы первой версии формата, где тип операции
        /// записывался полным именем типа .NET.
        /// </summary>
        private static readonly Dictionary<string, OperationDescriptor> ByName = BuildNameMap();

        private static Dictionary<string, OperationDescriptor> BuildNameMap()
        {
            var map = new Dictionary<string, OperationDescriptor>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in Descriptors)
            {
                map[descriptor.PersistentName] = descriptor;
                map[descriptor.OperationType.Name] = descriptor;
            }
            return map;
        }

        /// <summary>Все типы операций продукта.</summary>
        public static IReadOnlyList<OperationDescriptor> All => Descriptors;

        /// <summary>Типы операций указанной категории.</summary>
        public static IEnumerable<OperationDescriptor> ByCategory(OperationCategory category)
            => Descriptors.Where(descriptor => descriptor.Category == category);

        /// <summary>
        /// Описание типа операции. Бросает исключение для незарегистрированного
        /// типа: молчаливый пропуск означал бы потерю операции при сохранении
        /// или её отсутствие в программе.
        /// </summary>
        /// <param name="operationType">Точный тип операции.</param>
        public static OperationDescriptor ForType(Type operationType)
        {
            if (operationType == null)
                throw new ArgumentNullException(nameof(operationType));
            if (ByOperationType.TryGetValue(operationType, out var descriptor))
                return descriptor;

            throw new NotSupportedException($"Тип операции {operationType.FullName} отсутствует в каталоге операций.");
        }

        /// <summary>Описание типа операции или <c>null</c>, если тип не зарегистрирован.</summary>
        public static OperationDescriptor FindByType(Type operationType)
            => operationType != null && ByOperationType.TryGetValue(operationType, out var descriptor)
                ? descriptor
                : null;

        /// <summary>
        /// Описание типа по имени из файла проекта (имя каталога или имя класса
        /// из первой версии формата). <c>null</c> — имя неизвестно.
        /// </summary>
        public static OperationDescriptor FindByPersistentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            return ByName.TryGetValue(name.Trim(), out var descriptor) ? descriptor : null;
        }
    }
}
