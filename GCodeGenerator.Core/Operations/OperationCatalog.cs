using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.Operations
{
    /// <summary>
    /// Описание одного типа операции: всё, что о нём нужно знать общим
    /// механизмам — файлу проекта, выбору генератора, построению геометрии
    /// и очертанию на схеме.
    /// </summary>
    public sealed class OperationDescriptor
    {
        internal OperationDescriptor(
            Type operationType,
            string persistentName,
            OperationCategory category,
            Func<OperationBase> create,
            Func<OperationBase, IEnumerable<OperationOutline>> outlines,
            Func<OperationBase, IProfileGeometry> createProfileGeometry = null,
            Func<OperationBase, IPocketGeometry> createPocketGeometry = null)
        {
            OperationType = operationType;
            PersistentName = persistentName;
            Category = category;
            Create = create;
            Outlines = outlines;
            CreateProfileGeometry = createProfileGeometry;
            CreatePocketGeometry = createPocketGeometry;
        }

        /// <summary>Тип модели операции.</summary>
        public Type OperationType { get; }

        /// <summary>
        /// Имя типа в файле проекта. Не совпадает с именем класса намеренно:
        /// переименование класса не должно ломать чтение сохранённых проектов.
        /// </summary>
        public string PersistentName { get; }

        /// <summary>
        /// Ключ названия операции по умолчанию в словаре перевода: имя, под
        /// которым операция появляется в списке сразу после добавления.
        /// </summary>
        public string NameKey => PersistentName + "Name";

        /// <summary>Категория: сверление, профиль или карман.</summary>
        public OperationCategory Category { get; }

        /// <summary>Создаёт операцию этого типа со значениями по умолчанию.</summary>
        public Func<OperationBase> Create { get; }

        /// <summary>
        /// Очертание операции на плоскости: точки отверстий или контуры.
        /// Задано для каждого типа — иначе операция молча пропала бы со схемы.
        /// </summary>
        public Func<OperationBase, IEnumerable<OperationOutline>> Outlines { get; }

        /// <summary>
        /// Построение геометрии профиля; <c>null</c> для операций других
        /// категорий.
        /// </summary>
        public Func<OperationBase, IProfileGeometry> CreateProfileGeometry { get; }

        /// <summary>
        /// Построение геометрии кармана; <c>null</c> для операций других
        /// категорий.
        /// </summary>
        public Func<OperationBase, IPocketGeometry> CreatePocketGeometry { get; }
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
    /// Теперь тип операции описан одной строкой каталога: имя в файле проекта,
    /// категория, создание, геометрия и очертание на схеме. Остальные
    /// механизмы либо строятся от каталога, либо проверяются по нему тестами
    /// покрытия; диалог редактора регистрируется приложением, потому что
    /// ядро об окнах не знает.
    ///
    /// Каталог стоит над моделями и геометрией: он их связывает, поэтому
    /// ссылается на оба слоя, а они на него — нет.
    /// </summary>
    public static class OperationCatalog
    {
        private static readonly OperationDescriptor[] Descriptors =
        {
            // Сверление: девять режимов — один тип операции (режим в DrillMode).
            new OperationDescriptor(typeof(DrillPointsOperation), "DrillPoints", OperationCategory.Drill,
                () => new DrillPointsOperation(),
                operation => DrillOutlines((DrillPointsOperation)operation)),

            // Профили.
            new OperationDescriptor(typeof(ProfileRectangleOperation), "ProfileRectangle", OperationCategory.Profile,
                () => new ProfileRectangleOperation(),
                ProfileOutlines,
                createProfileGeometry: operation => new RectangleProfileGeometry((ProfileRectangleOperation)operation)),
            new OperationDescriptor(typeof(ProfileRoundedRectangleOperation), "ProfileRoundedRectangle", OperationCategory.Profile,
                () => new ProfileRoundedRectangleOperation(),
                ProfileOutlines,
                createProfileGeometry: operation => new RoundedRectangleProfileGeometry((ProfileRoundedRectangleOperation)operation)),
            new OperationDescriptor(typeof(ProfileCircleOperation), "ProfileCircle", OperationCategory.Profile,
                () => new ProfileCircleOperation(),
                ProfileOutlines,
                createProfileGeometry: operation => new CircleProfileGeometry((ProfileCircleOperation)operation)),
            new OperationDescriptor(typeof(ProfileEllipseOperation), "ProfileEllipse", OperationCategory.Profile,
                () => new ProfileEllipseOperation(),
                ProfileOutlines,
                createProfileGeometry: operation => new EllipseProfileGeometry((ProfileEllipseOperation)operation)),
            new OperationDescriptor(typeof(ProfilePolygonOperation), "ProfilePolygon", OperationCategory.Profile,
                () => new ProfilePolygonOperation(),
                ProfileOutlines,
                createProfileGeometry: operation => new PolygonProfileGeometry((ProfilePolygonOperation)operation)),
            // Чертёж уже задаёт контуры: фабрика геометрии их сливает и смещает,
            // поэтому на схеме показываются исходные полилинии, а не результат.
            new OperationDescriptor(typeof(ProfileDxfOperation), "ProfileDxf", OperationCategory.Profile,
                () => new ProfileDxfOperation(),
                operation => PolylineOutlines(((ProfileDxfOperation)operation).Polylines, minimumPoints: 2, isArea: false),
                createProfileGeometry: operation => new DxfProfileGeometry((ProfileDxfOperation)operation)),

            // Карманы.
            new OperationDescriptor(typeof(PocketRectangleOperation), "PocketRectangle", OperationCategory.Pocket,
                () => new PocketRectangleOperation(),
                PocketOutlines,
                createPocketGeometry: operation => new RectanglePocketGeometry((PocketRectangleOperation)operation)),
            new OperationDescriptor(typeof(PocketCircleOperation), "PocketCircle", OperationCategory.Pocket,
                () => new PocketCircleOperation(),
                PocketOutlines,
                createPocketGeometry: operation => new CirclePocketGeometry((PocketCircleOperation)operation)),
            new OperationDescriptor(typeof(PocketEllipseOperation), "PocketEllipse", OperationCategory.Pocket,
                () => new PocketEllipseOperation(),
                PocketOutlines,
                createPocketGeometry: operation => new EllipsePocketGeometry((PocketEllipseOperation)operation)),
            new OperationDescriptor(typeof(PocketDxfOperation), "PocketDxf", OperationCategory.Pocket,
                () => new PocketDxfOperation(),
                operation => PolylineOutlines(((PocketDxfOperation)operation).ClosedContours, minimumPoints: 3, isArea: true),
                createPocketGeometry: operation => new DxfPocketGeometry((PocketDxfOperation)operation)),
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

        /// <summary>
        /// Геометрия профиля для операции. Прежде тип разбирала отдельная
        /// фабрика, перечислявшая те же типы во второй раз.
        /// </summary>
        public static IProfileGeometry CreateProfileGeometry(OperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var factory = ForType(operation.GetType()).CreateProfileGeometry;
            if (factory == null)
                throw new NotSupportedException(
                    $"Операция {operation.GetType().Name} не является профилем: геометрия профиля для неё не задана.");

            return factory(operation);
        }

        /// <summary>Геометрия кармана для операции.</summary>
        public static IPocketGeometry CreatePocketGeometry(OperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var factory = ForType(operation.GetType()).CreatePocketGeometry;
            if (factory == null)
                throw new NotSupportedException(
                    $"Операция {operation.GetType().Name} не является карманом: геометрия кармана для неё не задана.");

            return factory(operation);
        }

        /// <summary>
        /// Очертание операции на плоскости. Для незарегистрированного типа
        /// бросает исключение — на схеме такая операция просто не появилась бы.
        /// </summary>
        public static IEnumerable<OperationOutline> OutlinesOf(OperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            return ForType(operation.GetType()).Outlines(operation);
        }

        /// <summary>Сверление: точка на каждое отверстие.</summary>
        private static IEnumerable<OperationOutline> DrillOutlines(DrillPointsOperation operation)
        {
            foreach (var hole in operation.Holes)
            {
                if (hole == null)
                    continue;

                yield return new OperationOutline(
                    OperationOutlineKind.Points,
                    new[] { (hole.X, hole.Y) },
                    isArea: false);
            }
        }

        /// <summary>
        /// Профиль: контур по линии чертежа. Смещение инструмента нулевое —
        /// схема показывает сам контур, а не траекторию обхода.
        /// </summary>
        private static IEnumerable<OperationOutline> ProfileOutlines(OperationBase operation)
        {
            var profile = (IProfileOperation)operation;
            var points = CreateProfileGeometry(operation)
                .GetContourPoints(0, profile.Direction)
                .Select(point => (point.x, point.y))
                .ToList();

            if (points.Count > 0)
                yield return new OperationOutline(OperationOutlineKind.Contour, points, isArea: false);
        }

        /// <summary>Карман: контур области выборки без учёта инструмента и уклона.</summary>
        private static IEnumerable<OperationOutline> PocketOutlines(OperationBase operation)
        {
            var points = CreatePocketGeometry(operation)
                .GetContour(0, 0)
                .GetPoints()
                .Select(point => (point.x, point.y))
                .ToList();

            if (points.Count >= 3)
                yield return new OperationOutline(OperationOutlineKind.Contour, points, isArea: true);
        }

        /// <summary>Контуры, пришедшие из чертежа, — как есть.</summary>
        private static IEnumerable<OperationOutline> PolylineOutlines(
            IEnumerable<Polyline2D> polylines, int minimumPoints, bool isArea)
        {
            if (polylines == null)
                yield break;

            foreach (var polyline in polylines)
            {
                if (polyline?.Points == null || polyline.Points.Count < minimumPoints)
                    continue;

                var points = new List<(double X, double Y)>(polyline.Points.Count);
                foreach (var point in polyline.Points)
                    points.Add((point.X, point.Y));

                yield return new OperationOutline(OperationOutlineKind.Contour, points, isArea);
            }
        }
    }
}
