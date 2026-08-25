using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Концентрическая стратегия обработки кармана (пункт 5.2 плана).
    /// Вложенные проходы вдоль эквидистантного контура: каждый проход —
    /// замкнутый контур, смещённый внутрь на k*step от стены
    /// (траектория центра инструмента = <c>GetContour(toolRadius + k*step, taperOffset)</c>).
    ///
    /// Остановка: когда смещённый контур становится «слишком маленьким»
    /// (<see cref="IPocketGeometry.IsContourTooSmall"/> — порог 5% диаметра
    /// эффективной фрезы) либо когда допустимый радиус исчерпан.
    /// Центральная область меньше порога остаётся необработанной
    /// (допускается для черновой обработки; чистовая — 5.6).
    ///
    /// Направление фрезерования: порядок точек контура — против часовой
    /// стрелки; для <see cref="MillingDirection.Clockwise"/> порядок разворачивается.
    /// </summary>
    public sealed class ConcentricPocketingStrategy : IPocketPocketingStrategy
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

            // Максимальное расстояние от центра до контура — страховочный предел смещения
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

            bool clockwise = op.Direction == MillingDirection.Clockwise;
            const double tolerance = GeometryTolerances.Degenerate;

            double offset = 0.0; // дополнительное смещение от стены
            int safetyLimit = 10000;
            while (safetyLimit-- > 0)
            {
                double effectiveToolRadius = toolRadius + offset;

                // Контур прохода слишком маленький — прекращаем
                if (geometry.IsContourTooSmall(effectiveToolRadius, taperOffset))
                    break;

                var contour = geometry.GetContour(effectiveToolRadius, taperOffset);
                if (contour == null)
                    break;

                var points = contour.GetPoints().ToList();
                if (points.Count < 3)
                    break;

                if (clockwise)
                    points.Reverse();

                // Фрезеруем замкнутый контур (инструмент на рабочей Z)
                foreach (var point in points)
                {
                    builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork, decimals: decimals);
                }

                // Замыкаем контур, если первая точка не совпадает с последней
                var first = points[0];
                var last = points[points.Count - 1];
                if (Math.Abs(first.x - last.x) > tolerance || Math.Abs(first.y - last.y) > tolerance)
                {
                    builder.LinearTo(x: first.x, y: first.y, feed: op.FeedXYWork, decimals: decimals);
                }

                offset += step;
                if (offset >= maxDistance)
                    break;
            }
        }
    }
}
