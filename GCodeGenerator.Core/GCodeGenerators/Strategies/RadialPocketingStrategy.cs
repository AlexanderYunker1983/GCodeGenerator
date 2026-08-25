using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

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
        public void MillContour(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            double step,
            double workingZ,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            ProgramBuilder builder,
            GCodeSettings settings)
        {
            // Стратегия работает на рабочей Z без отводов — workingZ не используется.
            int decimals = op.Decimals;

            if (contourPoints == null || contourPoints.Count == 0 || step <= 0)
                return;

            // Максимальное расстояние от центра до контура
            double maxDistance = 0.0;
            foreach (var point in contourPoints)
            {
                double dx = point.x - center.x;
                double dy = point.y - center.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > maxDistance)
                    maxDistance = distance;
            }
            if (maxDistance <= 0)
                return;

            // Число лучей: зазор на границе ≤ step
            double stepAngle = step / maxDistance;
            int spokes = Math.Max(2, (int)Math.Ceiling(2.0 * Math.PI / stepAngle));

            for (int i = 0; i < spokes; i++)
            {
                double theta = 2.0 * Math.PI * i / spokes;
                var boundary = FarthestRayIntersection(center, theta, contourPoints);

                // Проход: центр → граница → центр
                builder.LinearTo(x: boundary.x, y: boundary.y, feed: op.FeedXYWork, decimals: decimals);
                builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
            }
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
