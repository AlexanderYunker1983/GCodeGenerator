#nullable enable
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
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
            // Сплошная область проходится на рабочей Z без отводов; разорванная
            // островом требует повторного входа в слой (PocketLayerEntry).
            if (layer.ContourPoints == null || layer.ContourPoints.Count == 0 || layer.Step <= 0)
                return;

            // Максимальное расстояние от центра до контура — страховочный предел смещения
            double maxDistance = layer.MaxContourDistanceFromCenter();
            if (maxDistance <= 0)
                return;

            bool clockwise = op.Direction == MillingDirection.Clockwise;
            const double tolerance = GeometryTolerances.Degenerate;

            var contours = new List<PassContour>();
            // Смещение растёт на шаг, шаг положителен (проверен выше), а
            // предел конечен — значит проходов не больше, чем укладывается
            // шагов в расстояние от центра до стенки. Прежде здесь стоял
            // счётчик на десять тысяч оборотов: он молча обрывал обработку
            // очень большого кармана и скрывал бы ошибку, из-за которой
            // смещение перестало расти.
            for (double offset = 0.0; offset < maxDistance; offset += layer.Step)
            {
                if (!TryBuildPass(layer, offset, clockwise, out var passContours))
                    break;
                contours.AddRange(passContours);
            }

            // У острова эквидистанты растут навстречу друг другу: внешний
            // контур сжимается, внутренний расширяется. Поэтому для хода
            // снаружи внутрь сначала проходим внешнюю ветвь с ростом отступа,
            // затем внутреннюю — с его уменьшением до итоговой детали.
            // Обратное направление разворачивает обе части последовательности.
            IEnumerable<PassContour> ordered;
            if (op.ProcessingDirection == PocketProcessingDirection.CenterOutward)
            {
                ordered = contours.Where(contour => contour.IsHole)
                    .OrderBy(contour => contour.Offset)
                    .Concat(contours.Where(contour => !contour.IsHole)
                        .OrderByDescending(contour => contour.Offset));
            }
            else
            {
                ordered = contours.Where(contour => !contour.IsHole)
                    .OrderBy(contour => contour.Offset)
                    .Concat(contours.Where(contour => contour.IsHole)
                        .OrderByDescending(contour => contour.Offset));
            }
            foreach (var contour in ordered)
                EmitContour(layer, contour.Points, builder, tolerance);
        }

        private static bool TryBuildPass(
            PocketLayerContext layer,
            double offset,
            bool clockwise,
            out List<PassContour> contours)
        {
            contours = new List<PassContour>();
            double effectiveToolRadius = layer.ContourOffset + offset;

            if (layer.Geometry.IsContourTooSmall(effectiveToolRadius, layer.TaperOffset))
                return false;

            var contour = layer.Geometry.GetContour(effectiveToolRadius, layer.TaperOffset);
            if (contour == null)
                return false;

            var source = layer.RequiresSafeTransitions
                ? PocketGeometryContours.Get(layer.Geometry, effectiveToolRadius, layer.TaperOffset)
                : new[] { contour };
            if (source.Count == 0)
                return false;

            foreach (var passContour in source)
            {
                var points = passContour.GetPoints().ToList();
                if (points.Count < 3)
                    continue;

                var isHole = layer.RequiresSafeTransitions && SignedArea(points) < 0;
                if (clockwise)
                    points.Reverse();
                contours.Add(new PassContour(points, offset, isHole));
            }
            return contours.Count > 0;
        }

        private static double SignedArea(IReadOnlyList<(double x, double y)> points)
        {
            double doubledArea = 0;
            for (var index = 0; index < points.Count; index++)
            {
                var next = (index + 1) % points.Count;
                doubledArea += points[index].x * points[next].y
                               - points[next].x * points[index].y;
            }
            return doubledArea / 2.0;
        }

        private static void EmitContour(
            PocketLayerContext layer,
            List<(double x, double y)> points,
            ToolPathBuilder builder,
            double tolerance)
        {
            var op = layer.Operation;
            if (layer.RequiresSafeTransitions)
                PocketLayerEntry.Enter(layer, builder, points[0].x, points[0].y);

            foreach (var point in points)
                builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);

            GCodeGenerationHelper.CloseContour(builder, points, op.FeedXYWork, tolerance);
        }

        private sealed class PassContour
        {
            public PassContour(List<(double x, double y)> points, double offset, bool isHole)
            {
                Points = points;
                Offset = offset;
                IsHole = isHole;
            }

            public List<(double x, double y)> Points { get; }

            public double Offset { get; }

            public bool IsHole { get; }
        }
    }
}
