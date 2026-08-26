#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для эллиптического профиля.
    ///
    /// Эквидистанта считается честно — смещением контура через
    /// <see cref="ContourOffset"/>. Прежняя реализация прибавляла смещение
    /// к полуосям, но увеличенный эллипс — не эквидистанта эллипса: точка
    /// сдвигается радиально, а не по нормали, и в средних участках квадрантов
    /// траектория подходила к детали ближе радиуса фрезы — при наружной
    /// и внутренней обработке это зарез, доходивший до миллиметра на
    /// эллипсе 30×16 с фрезой D10.
    /// </summary>
    public class EllipseProfileGeometry : IProfileGeometry
    {
        /// <summary>Обычная фигура: один контур, который обходит генератор.</summary>
        public bool ProvidesOrderedContours => false;

        /// <inheritdoc />
        public IReadOnlyList<IReadOnlyList<(double x, double y)>> GetOrderedContours(double tolerance)
            => Array.Empty<IReadOnlyList<(double x, double y)>>();

        private readonly ProfileEllipseOperation _operation;

        // Кеш эквидистанты. Генератор запрашивает точки контура, старт,
        // периметр и точки рампы на одном и том же смещении десятки раз
        // за слой, а смещение через Clipper линейно по числу вершин.
        // Смещение меняется только вместе с режимом траектории, поэтому
        // хранится ровно одно значение.
        private bool _hasCachedOffset;
        private double _cachedOffsetValue;
        private List<(double x, double y)> _cachedOffsetPoints = new List<(double x, double y)>();

        public EllipseProfileGeometry(ProfileEllipseOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false; // Эллипс не может быть представлен как дуга в G-коде

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            var points = OffsetPoints(toolOffset);
            if (points.Count == 0)
                yield break;

            if (direction == MillingDirection.Clockwise)
            {
                // По часовой стрелке: тот же старт, обратный обход.
                yield return points[0];
                for (int i = points.Count - 1; i >= 1; i--)
                    yield return points[i];
            }
            else
            {
                for (int i = 0; i < points.Count; i++)
                    yield return points[i];
            }

            // Замыкаем контур — возвращаемся к начальной точке.
            yield return points[0];
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            var points = OffsetPoints(toolOffset);
            return points.Count > 0
                ? points[0]
                : (_operation.CenterX, _operation.CenterY);
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var points = OffsetPoints(toolOffset);
            if (points.Count == 0)
                return (_operation.CenterX, _operation.CenterY);

            var perimeter = GetPerimeter(toolOffset);
            var normalized = distance % perimeter;
            if (normalized < 0)
                normalized += perimeter;

            // Обход в направлении фрезеровки: рампа идёт по тому же контуру,
            // что и рабочий проход. Расстояние отсчитывается по фактической
            // ломаной — прежнее линейное отображение длины дуги в угол
            // распределяло точки рампы по эллипсу неравномерно.
            double accumulated = 0.0;
            var count = points.Count;
            for (int step = 0; step < count; step++)
            {
                var from = ContourPointAt(points, step);
                var to = ContourPointAt(points, step + 1);
                var segment = Geometry2D.Distance(from.x, from.y, to.x, to.y);
                if (accumulated + segment >= normalized && segment > 0)
                {
                    var t = (normalized - accumulated) / segment;
                    return (from.x + t * (to.x - from.x), from.y + t * (to.y - from.y));
                }

                accumulated += segment;
            }

            return points[0];
        }

        /// <inheritdoc />
        public IReadOnlyList<double> GetCornerDistances(double toolOffset)
            => ContourCornerDistances.FromPolyline(OffsetPoints(toolOffset));

        public double GetPerimeter(double toolOffset)        {
            var points = OffsetPoints(toolOffset);
            if (points.Count < 2)
                return 0.0;

            double perimeter = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                var from = points[i];
                var to = points[(i + 1) % points.Count];
                perimeter += Geometry2D.Distance(from.x, from.y, to.x, to.y);
            }

            return perimeter;
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // Эллипс не может быть представлен как дуга в G-коде
        }

        /// <summary>Точка контура в направлении обхода операции, с замыканием по кругу.</summary>
        private (double x, double y) ContourPointAt(List<(double x, double y)> points, int step)
        {
            var index = step % points.Count;
            if (_operation.Direction == MillingDirection.Clockwise)
                index = (points.Count - index) % points.Count;
            return points[index];
        }

        /// <summary>
        /// Точки эквидистанты: против часовой стрелки, без замыкающего
        /// дубликата, старт — у конца большой полуоси (там нормаль эллипса
        /// направлена вдоль неё, и стартовая точка совпадает с прежней).
        /// </summary>
        private List<(double x, double y)> OffsetPoints(double toolOffset)
        {
            if (_hasCachedOffset && _cachedOffsetValue.Equals(toolOffset))
                return _cachedOffsetPoints;

            var basePoints = TessellateBase(toolOffset);
            List<Point2D> contour;
            if (toolOffset == 0.0)
            {
                // Режим «по линии»: траектория — сама фигура, смещать нечего.
                contour = basePoints;
            }
            else
            {
                var parts = ContourOffset.Offset(basePoints, toolOffset);
                contour = LargestPart(parts);
                if (contour.Count == 0)
                {
                    // Смещение внутрь поглотило фигуру целиком: фреза такого
                    // радиуса внутри эллипса не помещается. Подставить
                    // «маленький эллипс», как раньше, значит выдать не ту
                    // траекторию — честный отказ называет причину.
                    throw new CoreException(CoreErrorCodes.EllipseToolDoesNotFit,
                        "The tool does not fit inside the ellipse: the offset contour is empty. "
                        + "Use a smaller tool or a larger ellipse.");
                }
            }

            var points = new List<(double x, double y)>(contour.Count);
            foreach (var point in contour)
                points.Add((point.X, point.Y));

            // Clipper не гарантирует ни направление, ни начальную вершину.
            if (Geometry2D.SignedArea(contour) < 0)
                points.Reverse();
            RotateToAnchor(points, toolOffset);

            _cachedOffsetPoints = points;
            _cachedOffsetValue = toolOffset;
            _hasCachedOffset = true;
            return points;
        }

        /// <summary>
        /// Тесселяция самого эллипса — вход для смещения. Плотность выбрана
        /// по периметру будущей эквидистанты, чтобы наружное смещение не
        /// растягивало шаг сверх MaxSegmentLength.
        /// </summary>
        private List<Point2D> TessellateBase(double toolOffset)
        {
            var estimateA = _operation.RadiusX + Math.Max(0.0, toolOffset);
            var estimateB = _operation.RadiusY + Math.Max(0.0, toolOffset);
            var h = Math.Pow(estimateA - estimateB, 2) / Math.Pow(estimateA + estimateB, 2);
            var perimeter = Math.PI * (estimateA + estimateB) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            var numSegments = Math.Max(8, (int)Math.Ceiling(perimeter / _operation.MaxSegmentLength));

            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var cosRot = Math.Cos(rotationRad);
            var sinRot = Math.Sin(rotationRad);

            var points = new List<Point2D>(numSegments);
            for (int i = 0; i < numSegments; i++)
            {
                var angle = 2 * Math.PI * i / numSegments;
                var xEllipse = _operation.RadiusX * Math.Cos(angle);
                var yEllipse = _operation.RadiusY * Math.Sin(angle);
                points.Add(new Point2D
                {
                    X = _operation.CenterX + xEllipse * cosRot - yEllipse * sinRot,
                    Y = _operation.CenterY + xEllipse * sinRot + yEllipse * cosRot,
                });
            }

            return points;
        }

        private static List<Point2D> LargestPart(List<List<Point2D>> parts)
        {
            var largest = new List<Point2D>();
            double largestArea = 0.0;
            foreach (var part in parts)
            {
                var area = Geometry2D.Area(part);
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = part;
                }
            }

            return largest;
        }

        /// <summary>
        /// Переставляет начало списка к вершине у конца большой полуоси:
        /// в этой точке нормаль эллипса направлена вдоль полуоси, поэтому
        /// стартовая точка эквидистанты лежит на её продолжении — там же,
        /// где начинался обход и до исправления эквидистанты.
        /// </summary>
        private void RotateToAnchor(List<(double x, double y)> points, double toolOffset)
        {
            if (points.Count < 2)
                return;

            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var anchorRadius = _operation.RadiusX + toolOffset;
            var anchorX = _operation.CenterX + anchorRadius * Math.Cos(rotationRad);
            var anchorY = _operation.CenterY + anchorRadius * Math.Sin(rotationRad);

            var anchorIndex = Geometry2D.ClosestVertexIndex(points, anchorX, anchorY);
            if (anchorIndex <= 0)
                return;

            var rotated = new List<(double x, double y)>(points.Count);
            for (int i = 0; i < points.Count; i++)
                rotated.Add(points[(anchorIndex + i) % points.Count]);
            points.Clear();
            points.AddRange(rotated);
        }
    }
}
