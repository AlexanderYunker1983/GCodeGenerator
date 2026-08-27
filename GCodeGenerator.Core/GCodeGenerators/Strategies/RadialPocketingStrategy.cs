#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Радиальная стратегия обработки кармана (пункт 5.3 плана).
    /// Лучи («солнечные спицы») от центра к контуру: каждый луч — проход
    /// центр → граница → центр на рабочей Z.
    ///
    /// Угловой шаг выбран так, чтобы зазор между соседними лучами на
    /// границе (максимальный радиус) не превышал <c>step</c>:
    /// число лучей = max(2, ceil(2π / (step / Rmax))), углы — равномерные.
    ///
    /// Направление фрезерования: каждый луч фрезеруется при движении
    /// от центра к границе (и в обратную сторону при возврате) —
    /// climb/conventional различие для радиального прохода не определяется.
    /// </summary>
    public sealed class RadialPocketingStrategy : IPocketPocketingStrategy
    {
        public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
        {
            var op = layer.Operation;
            // Стратегия работает на рабочей Z без отводов — workingZ не используется.
            if (layer.ContourPoints == null || layer.ContourPoints.Count == 0 || layer.Step <= 0)
                return;

            // Максимальное расстояние от центра до контура
            double maxDistance = layer.MaxContourDistanceFromCenter();
            if (maxDistance <= 0)
                return;

            // Число лучей: зазор на границе ≤ step
            double stepAngle = layer.Step / maxDistance;
            int spokes = Math.Max(2, (int)Math.Ceiling(2.0 * Math.PI / stepAngle));

            for (int i = 0; i < spokes; i++)
            {
                double theta = 2.0 * Math.PI * i / spokes;

                if (layer.RequiresSafeTransitions)
                {
                    foreach (var segment in RaySegments(layer, theta))
                    {
                        var from = (
                            x: layer.Center.x + segment.from * Math.Cos(theta),
                            y: layer.Center.y + segment.from * Math.Sin(theta));
                        var to = (
                            x: layer.Center.x + segment.to * Math.Cos(theta),
                            y: layer.Center.y + segment.to * Math.Sin(theta));

                        builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);
                        builder.RapidTo(x: from.x, y: from.y, feed: op.FeedXYRapid);
                        builder.RapidTo(z: layer.WorkingZ, feed: op.FeedZRapid);
                        builder.LinearTo(x: to.x, y: to.y, feed: op.FeedXYWork);
                        builder.LinearTo(x: from.x, y: from.y, feed: op.FeedXYWork);
                    }
                    continue;
                }

                var boundary = FarthestRayIntersection(layer.Center, theta, layer.ContourPoints);

                // Проход: центр → граница → центр
                builder.LinearTo(x: boundary.x, y: boundary.y, feed: op.FeedXYWork);
                builder.LinearTo(x: layer.Center.x, y: layer.Center.y, feed: op.FeedXYWork);
            }
        }

        /// <summary>
        /// Допустимые участки луча при наличии островов. Все пересечения со
        /// внешним и внутренними контурами сортируются по расстоянию, а
        /// принадлежность промежутка проверяется по его середине. Это не даёт
        /// лучу пересечь остров и сохраняет обработку участка за ним.
        /// </summary>
        private static List<(double from, double to)> RaySegments(
            PocketLayerContext layer,
            double theta)
        {
            var dx = Math.Cos(theta);
            var dy = Math.Sin(theta);
            var intersections = new List<double> { 0.0 };
            const double eps = GeometryTolerances.Degenerate;

            foreach (var contour in layer.BoundaryContours)
            {
                for (var index = 0; index < contour.Count; index++)
                {
                    var p1 = contour[index];
                    var p2 = contour[(index + 1) % contour.Count];
                    var ex = p2.x - p1.x;
                    var ey = p2.y - p1.y;
                    var denom = ex * dy - dx * ey;
                    if (Math.Abs(denom) < eps)
                        continue;

                    var wx = p1.x - layer.Center.x;
                    var wy = p1.y - layer.Center.y;
                    var t = (ex * wy - wx * ey) / denom;
                    var u = (dx * wy - wx * dy) / denom;
                    if (t >= -GeometryTolerances.Vertex
                        && u >= -GeometryTolerances.Vertex
                        && u <= 1.0 + GeometryTolerances.Vertex)
                    {
                        intersections.Add(Math.Max(0, t));
                    }
                }
            }

            intersections.Sort();
            var unique = new List<double>();
            foreach (var distance in intersections)
            {
                if (unique.Count == 0
                    || Math.Abs(distance - unique[unique.Count - 1]) > GeometryTolerances.Vertex)
                {
                    unique.Add(distance);
                }
            }

            var result = new List<(double from, double to)>();
            for (var index = 0; index + 1 < unique.Count; index++)
            {
                var from = unique[index];
                var to = unique[index + 1];
                if (to - from <= GeometryTolerances.Vertex)
                    continue;

                var middle = (from + to) / 2.0;
                if (layer.Geometry.IsPointInside(
                        layer.Center.x + middle * dx,
                        layer.Center.y + middle * dy,
                        layer.ContourOffset,
                        layer.TaperOffset))
                {
                    result.Add((from, to));
                }
            }
            return result;
        }

        /// <summary>
        /// Дальнейшая точка пересечения луча (центр, угол θ) с замкнутым
        /// контуром — точка на границе в направлении луча.
        /// </summary>
        private static (double x, double y) FarthestRayIntersection(
            (double x, double y) center,
            double theta,
            List<(double x, double y)> contourPoints)
        {
            double dx = Math.Cos(theta);
            double dy = Math.Sin(theta);
            const double eps = GeometryTolerances.Degenerate;

            double bestT = 0.0;
            for (int i = 0; i < contourPoints.Count; i++)
            {
                var p1 = contourPoints[i];
                var p2 = contourPoints[(i + 1) % contourPoints.Count];

                double ex = p2.x - p1.x;
                double ey = p2.y - p1.y;

                // Луч: C + t*d; сегмент: P1 + u*e. Система t*d - u*e = w решается
                // правилом Крамера: det = ex*dy - dx*ey.
                double denom = ex * dy - dx * ey;
                if (Math.Abs(denom) < eps)
                    continue; // параллельны

                double wx = p1.x - center.x;
                double wy = p1.y - center.y;

                double t = (ex * wy - wx * ey) / denom;
                double u = (dx * wy - wx * dy) / denom;

                if (t > eps && u >= -GeometryTolerances.Vertex && u <= 1.0 + GeometryTolerances.Vertex)
                {
                    if (t > bestT)
                        bestT = t;
                }
            }

            if (bestT > 0.0)
                return (center.x + bestT * dx, center.y + bestT * dy);

            // Фолбэк для вырожденного контура: самая дальняя точка
            double maxDist = 0.0;
            (double x, double y) farthest = contourPoints[0];
            foreach (var point in contourPoints)
            {
                double ddx = point.x - center.x;
                double ddy = point.y - center.y;
                double dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    farthest = point;
                }
            }
            return farthest;
        }
    }
}
