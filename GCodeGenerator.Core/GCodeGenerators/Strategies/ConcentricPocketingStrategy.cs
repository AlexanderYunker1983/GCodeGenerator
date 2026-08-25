#nullable enable
using System;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Концентрическая стратегия обработки кармана (пункт 5.2 плана).
    /// Вложенные проходы вдоль эквидистантного контура: каждый проход —
    /// замкнутый контур, смещённый внутрь на k*step от стены
    /// (траектория центра инструмента = <c>GetContour(ContourOffset + k*step, taperOffset)</c>,
    /// где ContourOffset — радиус фрезы вместе с припуском).
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
        public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
        {
            var op = layer.Operation;
            // Стратегия работает на рабочей Z без отводов — workingZ не используется.
            int decimals = op.Decimals;

            if (layer.ContourPoints == null || layer.ContourPoints.Count == 0 || layer.Step <= 0)
                return;

            // Максимальное расстояние от центра до контура — страховочный предел смещения
            double maxDistance = 0.0;
            foreach (var point in layer.ContourPoints)
            {
                double dx = point.x - layer.Center.x;
                double dy = point.y - layer.Center.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > maxDistance)
                    maxDistance = distance;
            }
            if (maxDistance <= 0)
                return;

            bool clockwise = op.Direction == MillingDirection.Clockwise;
            const double tolerance = GeometryTolerances.Degenerate;

            // Смещение растёт на шаг, шаг положителен (проверен выше), а
            // предел конечен — значит проходов не больше, чем укладывается
            // шагов в расстояние от центра до стенки. Прежде здесь стоял
            // счётчик на десять тысяч оборотов: он молча обрывал обработку
            // очень большого кармана и скрывал бы ошибку, из-за которой
            // смещение перестало расти.
            for (double offset = 0.0; offset < maxDistance; offset += layer.Step)
            {
                double effectiveToolRadius = layer.ContourOffset + offset;

                // Контур прохода слишком маленький — прекращаем
                if (layer.Geometry.IsContourTooSmall(effectiveToolRadius, layer.TaperOffset))
                    break;

                var contour = layer.Geometry.GetContour(effectiveToolRadius, layer.TaperOffset);
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

            }
        }
    }
}
