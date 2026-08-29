#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для DXF кармана.
    /// Работает с замкнутыми контурами из DXF файла.
    ///
    /// Смещение контура на радиус инструмента выполняет <see cref="ContourOffset"/>.
    /// При смещении внутрь область может распасться на несколько частей (узкая
    /// перемычка исчезает раньше остального кармана), поэтому эквидистанта —
    /// это список контуров, а не один контур. Части перебирает
    /// <see cref="DxfPocketLayerGenerator"/>; методы этого класса работают
    /// со всеми частями сразу.
    /// </summary>
    public class DxfPocketGeometry : IPocketGeometry
    {
        /// <summary>
        /// Чертёж может задать карман с перемычками: смещение внутрь
        /// разбивает его на отдельные области, каждая из которых
        /// фрезеруется как самостоятельный карман.
        /// </summary>
        public bool SplitsIntoAreas => true;

        /// <summary>
        /// DXF допускает произвольный вогнутый контур. Две точки внутри него
        /// не гарантируют, что соединяющий их отрезок тоже лежит внутри,
        /// поэтому для такой области стратегии не имеют права связывать
        /// проходы на рабочей Z. Выпуклые области сохраняют короткие связки.
        /// </summary>
        public bool RequiresSafeTransitions => IsConcave(_primaryContour?.Points);

        /// <inheritdoc />
        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
        {
            var areas = new List<IPocketGeometry>();
            if (_operation.ClosedContours == null)
                return areas;

            // Каждый замкнутый контур операции смещается внутрь сам по себе
            // и может дать несколько частей: узкая перемычка исчезает раньше
            // остального кармана.
            foreach (var contour in _operation.ClosedContours)
            {
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

                foreach (var part in new DxfPocketGeometry(_operation, contour)
                             .GetOffsetParts(toolRadius, taperOffset))
                {
                    if (part.Count < 3)
                        continue;

                    areas.Add(new DxfPocketGeometry(
                        _operation,
                        new Polyline2D { Points = new List<Point2D>(part) }));
                }
            }

            return areas;
        }

        /// <summary>Операция: её замкнутые контуры образуют области кармана.</summary>
        private readonly PocketDxfOperation _operation;

        /// <summary>
        /// Контур, который обрабатывает эта геометрия. Пусто, если операция
        /// пришла без контуров: тогда обрабатывать нечего, и это состояние
        /// проверка операции отклонит до генерации.
        /// </summary>
        private readonly Polyline2D? _primaryContour;

        // Кеш последней построенной эквидистанты и центра исходного контура.
        // В пределах одного слоя смещение одинаково для всех вызовов
        // (GetContour, IsContourTooSmall и IsPointInside на каждую точку
        // траектории стратегии), а построение эквидистанты линейно по числу
        // вершин. Смещение меняется между слоями, поэтому кеш хранит ровно
        // одно значение и пересчитывается при его смене; точки контура за
        // время жизни экземпляра не меняются.
        private bool _hasCachedOffset;
        private double _cachedOffsetValue;
        private List<List<Point2D>>? _cachedOffsetParts;
        private bool _hasCachedCenter;
        private (double x, double y) _cachedCenter;

        public DxfPocketGeometry(PocketDxfOperation operation, Polyline2D? primaryContour = null)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            _operation = operation;

            // Используем первый контур как основной, если не указан явно
            _primaryContour = primaryContour ??
                (operation.ClosedContours != null && operation.ClosedContours.Count > 0
                    ? operation.ClosedContours[0]
                    : null);
        }

        private static bool IsConcave(IReadOnlyList<Point2D>? points)
        {
            if (points == null || points.Count < 4)
                return false;

            var count = points.Count;
            if (Geometry2D.PointsMatch(points[0], points[count - 1], GeometryTolerances.Vertex))
                count--;
            if (count < 4)
                return false;

            var orientation = 0;
            for (var index = 0; index < count; index++)
            {
                var a = points[index];
                var b = points[(index + 1) % count];
                var c = points[(index + 2) % count];
                var cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (Math.Abs(cross) <= GeometryTolerances.Degenerate)
                    continue;

                var current = cross > 0 ? 1 : -1;
                if (orientation == 0)
                    orientation = current;
                else if (orientation != current)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Части эквидистанты исходного контура, смещённой внутрь на
        /// <paramref name="toolRadius"/> плюс <paramref name="taperOffset"/>.
        /// Пустой список означает, что инструмент такого радиуса в контур
        /// не помещается.
        /// </summary>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок на глубине слоя.</param>
        public IReadOnlyList<IReadOnlyList<Point2D>> GetOffsetParts(double toolRadius, double taperOffset)
            => GetOffsetParts(-(toolRadius + taperOffset));

        private List<List<Point2D>> GetOffsetParts(double offset)
        {
            if (_hasCachedOffset && _cachedOffsetValue.Equals(offset) && _cachedOffsetParts != null)
                return _cachedOffsetParts;

            _cachedOffsetParts = _primaryContour?.Points == null
                ? new List<List<Point2D>>()
                : ContourOffset.Offset(_primaryContour.Points, offset);
            _cachedOffsetValue = offset;
            _hasCachedOffset = true;
            return _cachedOffsetParts;
        }

        public (double x, double y) GetCenter()
        {
            if (_hasCachedCenter)
                return _cachedCenter;

            _cachedCenter = Geometry2D.Centroid(_primaryContour?.Points, GeometryTolerances.Vertex);
            _hasCachedCenter = true;
            return _cachedCenter;
        }

        /// <summary>
        /// Наибольшая по площади часть эквидистанты. Полный набор частей даёт
        /// <see cref="GetOffsetParts(double, double)"/>: контракт
        /// <see cref="IPocketGeometry"/> описывает один контур, а карман из DXF
        /// при смещении может распасться на несколько.
        /// </summary>
        public IContour GetContour(double toolRadius, double taperOffset)
        {
            var parts = GetOffsetParts(toolRadius, taperOffset);
            if (parts.Count == 0)
                return new EmptyContour();

            var largest = parts[0];
            double largestArea = Geometry2D.Area(largest);
            for (int i = 1; i < parts.Count; i++)
            {
                double area = Geometry2D.Area(parts[i]);
                if (area > largestArea)
                {
                    largest = parts[i];
                    largestArea = area;
                }
            }

            return new DxfContour(largest);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            foreach (var part in GetOffsetParts(toolRadius, taperOffset))
            {
                if (Geometry2D.IsPointInsidePolygon(x, y, part))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Контур слишком мал, когда после смещения на радиус инструмента
        /// не остаётся ни одной области. Отдельных эвристик вырождения больше
        /// нет: смещение возвращает корректный результат, а пустой результат
        /// и есть «фреза не помещается».
        /// </summary>
        public bool IsContourTooSmall(double toolRadius, double taperOffset)
            => GetOffsetParts(toolRadius, taperOffset).Count == 0;

        /// <summary>
        /// Реализация контура из набора точек.
        /// </summary>
        private class DxfContour : IContour
        {
            private readonly IReadOnlyList<Point2D> _points;

            public DxfContour(IReadOnlyList<Point2D> points)
            {
                _points = points;
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                if (_points == null)
                    yield break;

                foreach (var point in _points)
                    yield return (point.X, point.Y);
            }

            public double GetArea() => Geometry2D.Area(_points);
        }

        /// <summary>
        /// Пустой контур для случаев, когда контур недоступен.
        /// </summary>
        private class EmptyContour : IContour
        {
            public IEnumerable<(double x, double y)> GetPoints()
            {
                yield break;
            }

            public double GetArea()
            {
                return 0;
            }
        }
    }
}
