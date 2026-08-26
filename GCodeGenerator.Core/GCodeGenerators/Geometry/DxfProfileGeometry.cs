#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для DXF профиля.
    ///
    /// Здесь находится единственный расчёт смещения профиля на радиус
    /// инструмента. Раньше такой же расчёт был продублирован в
    /// <see cref="UnifiedProfileGenerator"/>: генератор строил траекторию своей
    /// копией алгоритма, а вход в материал по рампе рассчитывался по этой
    /// геометрии, так что расхождение копий развело бы рампу и рез.
    ///
    /// Замкнутый контур смещает <see cref="ContourOffset"/> (Clipper2): у него
    /// вершина отходит на правильное расстояние по биссектрисе, тогда как
    /// смещение по усреднённой нормали срезало углы (на прямом угле — почти
    /// на 30 %). Для незамкнутой полилинии смещение области не определено,
    /// поэтому там по-прежнему используется усреднённая нормаль.
    /// </summary>
    public class DxfProfileGeometry : IProfileGeometry
    {
        /// <summary>
        /// Чертёж задаёт контуры сам: смещение уже расставило точки в порядке
        /// обхода, и таких контуров может быть несколько.
        /// </summary>
        public bool ProvidesOrderedContours => true;

        private readonly ProfileDxfOperation _operation;

        public DxfProfileGeometry(ProfileDxfOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false; // DXF может содержать дуги, но для упрощения считаем их линейными сегментами

        /// <summary>
        /// Смещение траектории, заданное режимом обработки операции.
        /// </summary>
        private double ToolOffset
        {
            get
            {
                var toolRadius = _operation.ToolDiameter / 2.0;
                if (_operation.ToolPathMode == ToolPathMode.Outside)
                    return toolRadius;
                if (_operation.ToolPathMode == ToolPathMode.Inside)
                    return -toolRadius;
                return 0.0;
            }
        }

        // Кеш смещённой геометрии, по образцу DxfPocketGeometry. Рампа
        // запрашивает точку контура на каждый свой сегмент, и без кеша
        // каждый запрос заново гонял смещение всех полилиний через Clipper —
        // тысячи полных прогонов на один вход в слой. Смещение зависит
        // только от режима траектории операции и за время жизни экземпляра
        // не меняется; цепочки контуров дополнительно зависят от допуска
        // стыковки, поэтому их кеш хранит свой ключ.
        private List<(double x, double y)>? _cachedContourPoints;
        private MillingDirection _cachedContourDirection;
        private IReadOnlyList<IReadOnlyList<(double x, double y)>>? _cachedOrderedContours;
        private double _cachedOrderedTolerance;

        /// <summary>Точки контура в направлении обхода — материализованные один раз.</summary>
        private List<(double x, double y)> CachedContourPoints(MillingDirection direction)
        {
            if (_cachedContourPoints == null || _cachedContourDirection != direction)
            {
                _cachedContourPoints = GetContourPoints(ToolOffset, direction).ToList();
                _cachedContourDirection = direction;
            }

            return _cachedContourPoints;
        }

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            if (_operation.Polylines == null || _operation.Polylines.Count == 0)
                yield break;

            foreach (var polyline in _operation.Polylines)
            {
                if (polyline?.Points == null || polyline.Points.Count < 2)
                    continue;

                foreach (var part in OffsetPolyline(polyline.Points, ToolOffset))
                {
                    var points = TrimClosingDuplicate(part);
                    if (direction == MillingDirection.Clockwise)
                    {
                        for (int i = points.Count - 1; i >= 0; i--)
                            yield return points[i];
                    }
                    else
                    {
                        for (int i = 0; i < points.Count; i++)
                            yield return points[i];
                    }
                }
            }
        }

        /// <summary>
        /// Замкнутые цепочки полилиний со смещением на радиус инструмента:
        /// полилинии, состыкованные концами, образуют один контур, который
        /// фрезеруется без отрыва инструмента. Возвращает по одному списку
        /// точек на контур — переходы между контурами добавляет генератор.
        /// </summary>
        /// <param name="tolerance">Допуск стыковки концов полилиний.</param>
        public IReadOnlyList<IReadOnlyList<(double x, double y)>> GetOrderedContours(double tolerance)
        {
            if (_cachedOrderedContours != null && _cachedOrderedTolerance.Equals(tolerance))
                return _cachedOrderedContours;

            _cachedOrderedContours = BuildOrderedContours(tolerance);
            _cachedOrderedTolerance = tolerance;
            return _cachedOrderedContours;
        }

        private IReadOnlyList<IReadOnlyList<(double x, double y)>> BuildOrderedContours(double tolerance)
        {
            var result = new List<IReadOnlyList<(double x, double y)>>();
            if (_operation.Polylines == null || _operation.Polylines.Count == 0)
                return result;

            double offset = ToolOffset;

            foreach (var chain in GroupPolylinesIntoContours(_operation.Polylines, tolerance))
            {
                var contourPoints = new List<(double x, double y)>();

                foreach (var polyline in chain)
                {
                    if (polyline?.Points == null || polyline.Points.Count < 2)
                        continue;

                    foreach (var part in OffsetPolyline(polyline.Points, offset))
                    {
                        var points = TrimClosingDuplicate(part);
                        if (points.Count == 0)
                            continue;

                        // Стык с предыдущей полилинией цепочки не должен давать
                        // повторную точку: инструмент уже стоит в ней.
                        int startIndex = 0;
                        if (contourPoints.Count > 0)
                        {
                            var last = contourPoints[contourPoints.Count - 1];
                            var first = points[0];
                            if (Math.Abs(last.x - first.x) < tolerance && Math.Abs(last.y - first.y) < tolerance)
                                startIndex = 1;
                        }

                        for (int i = startIndex; i < points.Count; i++)
                            contourPoints.Add(points[i]);
                    }
                }

                if (contourPoints.Count > 0)
                    result.Add(contourPoints);
            }

            return result;
        }

        /// <summary>
        /// Смещает одну полилинию. Замкнутая обрабатывается как область
        /// (результатом может быть несколько контуров, если она распалась),
        /// незамкнутая — сдвигом вершин по усреднённой нормали соседних сторон.
        /// Нулевое смещение возвращает исходные точки без изменений.
        /// </summary>
        private static IReadOnlyList<List<(double x, double y)>> OffsetPolyline(
            IReadOnlyList<Point2D> points,
            double offset)
        {
            var single = new List<List<(double x, double y)>>();

            if (offset == 0.0)
            {
                single.Add(points.Select(p => (p.X, p.Y)).ToList());
                return single;
            }

            if (IsClosed(points))
            {
                foreach (var part in ContourOffset.Offset(points, offset))
                    single.Add(part.Select(p => (p.X, p.Y)).ToList());
                return single;
            }

            single.Add(OffsetOpenPolyline(points, offset));
            return single;
        }

        /// <summary>
        /// Смещение незамкнутой полилинии: каждая вершина сдвигается по
        /// усреднённой нормали прилегающих сторон. Точного смещения области
        /// здесь не существует — у линии нет внутренней стороны.
        ///
        /// Концевые вершины смещаются по нормали своей единственной стороны.
        /// Прежний расчёт брал у первой вершины «предыдущей» последнюю точку
        /// полилинии, а у последней — первую, как если бы линия была замкнута:
        /// у прямого отрезка нормали получались противоположными, их среднее
        /// обращалось в ноль, и концы линии оставались несмещёнными.
        /// </summary>
        private static List<(double x, double y)> OffsetOpenPolyline(IReadOnlyList<Point2D> points, double offset)
        {
            const double tolerance = GeometryTolerances.Vertex;
            var offsetPoints = new List<(double x, double y)>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];

                // Нормали прилегающих сторон; у концевых вершин сторона одна.
                bool hasPrevious = i > 0;
                bool hasNext = i < points.Count - 1;

                double nx1 = 0, ny1 = 0, nx2 = 0, ny2 = 0;
                bool previousValid = false, nextValid = false;

                if (hasPrevious)
                {
                    var prevP = points[i - 1];
                    var dx = p.X - prevP.X;
                    var dy = p.Y - prevP.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > tolerance)
                    {
                        nx1 = -dy / len;
                        ny1 = dx / len;
                        previousValid = true;
                    }
                }

                if (hasNext)
                {
                    var nextP = points[i + 1];
                    var dx = nextP.X - p.X;
                    var dy = nextP.Y - p.Y;
                    var len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > tolerance)
                    {
                        nx2 = -dy / len;
                        ny2 = dx / len;
                        nextValid = true;
                    }
                }

                (double x, double y) offsetPoint;
                if (previousValid && nextValid)
                {
                    var nx = (nx1 + nx2) / 2.0;
                    var ny = (ny1 + ny2) / 2.0;
                    var nlen = Math.Sqrt(nx * nx + ny * ny);
                    if (nlen > tolerance)
                    {
                        nx /= nlen;
                        ny /= nlen;
                    }

                    offsetPoint = (p.X + nx * offset, p.Y + ny * offset);
                }
                else if (previousValid)
                {
                    offsetPoint = (p.X + nx1 * offset, p.Y + ny1 * offset);
                }
                else if (nextValid)
                {
                    offsetPoint = (p.X + nx2 * offset, p.Y + ny2 * offset);
                }
                else
                {
                    offsetPoint = (p.X, p.Y);
                }

                offsetPoints.Add(offsetPoint);
            }

            return offsetPoints;
        }

        /// <summary>
        /// Убирает замыкающую точку, совпадающую с первой: инструмент уже
        /// стоит в ней, а замыкание контура добавляет генератор.
        /// </summary>
        private static List<(double x, double y)> TrimClosingDuplicate(List<(double x, double y)> points)
        {
            const double tolerance = GeometryTolerances.Vertex;
            if (points.Count > 1
                && Math.Abs(points[0].x - points[points.Count - 1].x) < tolerance
                && Math.Abs(points[0].y - points[points.Count - 1].y) < tolerance)
            {
                points.RemoveAt(points.Count - 1);
            }
            return points;
        }

        private static bool IsClosed(IReadOnlyList<Point2D> points)
            => points.Count > 2
                && Geometry2D.PointsMatch(points[0], points[points.Count - 1], GeometryTolerances.Vertex);

        /// <summary>
        /// Группирует полилинии в цепочки по стыковке концов: отдельные
        /// отрезки и дуги из DXF складываются в контуры, которые фрезеруются
        /// без отрыва инструмента.
        /// </summary>
        private static List<List<Polyline2D>> GroupPolylinesIntoContours(List<Polyline2D> polylines, double tolerance)
        {
            var contours = new List<List<Polyline2D>>();
            var used = new bool[polylines.Count];

            for (int i = 0; i < polylines.Count; i++)
            {
                if (used[i] || polylines[i]?.Points == null || polylines[i].Points.Count < 2)
                    continue;

                var contour = BuildContourFromPolyline(polylines, i, used, tolerance);
                if (contour != null && contour.Count > 0)
                    contours.Add(contour);
            }

            return contours;
        }

        /// <summary>
        /// Строит цепочку, начиная с указанной полилинии: каждая следующая
        /// присоединяется концом к текущему концу цепочки, при необходимости
        /// разворачиваясь.
        /// </summary>
        private static List<Polyline2D> BuildContourFromPolyline(
            List<Polyline2D> polylines,
            int startIdx,
            bool[] used,
            double tolerance)
        {
            var contour = new List<Polyline2D> { polylines[startIdx] };
            used[startIdx] = true;

            var startPoint = polylines[startIdx].Points[0];
            var currentPoint = polylines[startIdx].Points[polylines[startIdx].Points.Count - 1];

            bool foundConnection = true;
            while (foundConnection)
            {
                foundConnection = false;

                for (int i = 0; i < polylines.Count; i++)
                {
                    if (used[i] || polylines[i]?.Points == null || polylines[i].Points.Count < 2)
                        continue;

                    var polyline = polylines[i];
                    var polyStart = polyline.Points[0];
                    var polyEnd = polyline.Points[polyline.Points.Count - 1];

                    if (Geometry2D.PointsMatch(currentPoint, polyStart, tolerance))
                    {
                        contour.Add(polyline);
                        used[i] = true;
                        currentPoint = polyEnd;
                        foundConnection = true;
                        break;
                    }

                    if (Geometry2D.PointsMatch(currentPoint, polyEnd, tolerance))
                    {
                        var reversedPolyline = new Polyline2D
                        {
                            Points = new List<Point2D>(polyline.Points)
                        };
                        reversedPolyline.Points.Reverse();
                        contour.Add(reversedPolyline);
                        used[i] = true;
                        currentPoint = polyStart;
                        foundConnection = true;
                        break;
                    }
                }

                if (Geometry2D.PointsMatch(currentPoint, startPoint, tolerance))
                    break;
            }

            return contour;
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            // Точка входа обязана лежать на смещённой траектории: подвод,
            // врезание, витки рампы и возвраты между ними выполняются в ней.
            // Прежде здесь возвращалась первая точка чертежа — центр фрезы
            // вставал прямо на кромку детали, врезание зарезало её на радиус
            // инструмента, а быстрый спуск между витками рампы бил в
            // нетронутый материал: колонку над точкой чертежа никто не режет,
            // режется колонка над точкой траектории. Точка согласована
            // с первым резом GenerateOrderedContours: тот же допуск стыковки
            // и тот же порядок обхода.
            var contours = GetOrderedContours(GeometryTolerances.Vertex);
            if (contours.Count == 0 || contours[0].Count == 0)
                return (0, 0);

            var first = contours[0];
            return _operation.Direction == MillingDirection.Clockwise
                ? first[first.Count - 1]
                : first[0];
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var points = CachedContourPoints(_operation.Direction);
            if (points.Count == 0)
                return (0, 0);

            var perimeter = GetPerimeter(toolOffset);
            var normalizedDistance = distance % perimeter;
            if (normalizedDistance < 0) normalizedDistance += perimeter;

            double accumulated = 0.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                var segmentLength = Math.Sqrt(Math.Pow(p2.x - p1.x, 2) + Math.Pow(p2.y - p1.y, 2));

                if (accumulated + segmentLength >= normalizedDistance)
                {
                    var t = (normalizedDistance - accumulated) / segmentLength;
                    return (p1.x + t * (p2.x - p1.x), p1.y + t * (p2.y - p1.y));
                }

                accumulated += segmentLength;
            }

            return points[0];
        }

        public double GetPerimeter(double toolOffset)
        {
            if (_operation.Polylines == null || _operation.Polylines.Count == 0)
                return 0.0;

            var perimeter = 0.0;
            foreach (var polyline in _operation.Polylines)
            {
                if (polyline?.Points == null || polyline.Points.Count < 2)
                    continue;

                for (int i = 0; i < polyline.Points.Count; i++)
                {
                    var p1 = polyline.Points[i];
                    var p2 = polyline.Points[(i + 1) % polyline.Points.Count];
                    var dx = p2.X - p1.X;
                    var dy = p2.Y - p1.Y;
                    perimeter += Math.Sqrt(dx * dx + dy * dy);
                }
            }

            // Упрощенная коррекция периметра с учетом смещения
            // В реальности нужно учитывать смещение по нормали, но для упрощения используем линейную аппроксимацию
            return perimeter + ToolOffset * 2 * Math.PI; // Примерная коррекция
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // DXF профили обрабатываются как линейные сегменты
        }
    }
}
