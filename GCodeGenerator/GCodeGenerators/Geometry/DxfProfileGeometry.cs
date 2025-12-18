using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для DXF профиля.
    /// </summary>
    public class DxfProfileGeometry : IProfileGeometry
    {
        private readonly ProfileDxfOperation _operation;

        public DxfProfileGeometry(ProfileDxfOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false; // DXF может содержать дуги, но для упрощения считаем их линейными сегментами

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            if (_operation.Polylines == null || _operation.Polylines.Count == 0)
                yield break;

            var toolRadius = _operation.ToolDiameter / 2.0;
            var offset = 0.0;
            if (_operation.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (_operation.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            foreach (var polyline in _operation.Polylines)
            {
                if (polyline?.Points == null || polyline.Points.Count < 2)
                    continue;

                // Применяем смещение к точкам полилинии
                // Для упрощения применяем смещение по нормали к каждому сегменту
                var points = polyline.Points;
                var offsetPoints = new List<(double x, double y)>();

                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    var prevP = i > 0 ? points[i - 1] : points[points.Count - 1];
                    var nextP = i < points.Count - 1 ? points[i + 1] : points[0];

                    // Вычисляем нормаль к сегменту
                    var dx1 = p.X - prevP.X;
                    var dy1 = p.Y - prevP.Y;
                    var dx2 = nextP.X - p.X;
                    var dy2 = nextP.Y - p.Y;

                    var len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                    var len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                    if (len1 > 1e-6 && len2 > 1e-6)
                    {
                        // Биссектриса угла
                        var nx1 = -dy1 / len1;
                        var ny1 = dx1 / len1;
                        var nx2 = -dy2 / len2;
                        var ny2 = dx2 / len2;

                        var nx = (nx1 + nx2) / 2.0;
                        var ny = (ny1 + ny2) / 2.0;
                        var nlen = Math.Sqrt(nx * nx + ny * ny);
                        if (nlen > 1e-6)
                        {
                            nx /= nlen;
                            ny /= nlen;
                        }

                        // Применяем смещение
                        var offsetX = p.X + nx * offset;
                        var offsetY = p.Y + ny * offset;
                        offsetPoints.Add((offsetX, offsetY));
                    }
                    else
                    {
                        offsetPoints.Add((p.X, p.Y));
                    }
                }

                // Генерируем точки в нужном направлении
                if (direction == MillingDirection.Clockwise)
                {
                    for (int i = offsetPoints.Count - 1; i >= 0; i--)
                    {
                        yield return offsetPoints[i];
                    }
                }
                else
                {
                    foreach (var point in offsetPoints)
                    {
                        yield return point;
                    }
                }
            }
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            if (_operation.Polylines == null || _operation.Polylines.Count == 0)
                return (0, 0);

            var firstPolyline = _operation.Polylines[0];
            if (firstPolyline?.Points == null || firstPolyline.Points.Count == 0)
                return (0, 0);

            var toolRadius = _operation.ToolDiameter / 2.0;
            var offset = 0.0;
            if (_operation.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (_operation.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            var firstPoint = firstPolyline.Points[0];
            return (firstPoint.X, firstPoint.Y); // Упрощенная версия без смещения для начальной точки
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var points = GetContourPoints(toolOffset, _operation.Direction).ToList();
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

            var toolRadius = _operation.ToolDiameter / 2.0;
            var offset = 0.0;
            if (_operation.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (_operation.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

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
            return perimeter + offset * 2 * Math.PI; // Примерная коррекция
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // DXF профили обрабатываются как линейные сегменты
        }
    }
}

