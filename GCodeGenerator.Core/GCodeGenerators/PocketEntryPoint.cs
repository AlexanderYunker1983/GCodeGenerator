#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Strategies;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Точка врезания в область кармана.
    ///
    /// Центроид годится только для выпуклых областей: у вогнутой — подковы,
    /// буквы «П» из чертежа — он может лежать вне области, и врезание в него
    /// было бы ударом инструмента в нетронутый материал за стенкой кармана.
    /// Карманы из чертежа существуют ради произвольных контуров, поэтому
    /// принадлежность точки области проверяется всегда, а для вогнутых
    /// берётся середина длиннейшего сегмента скан-линии — гарантированно
    /// внутренняя точка с наибольшим запасом по ширине.
    /// </summary>
    internal static class PocketEntryPoint
    {
        /// <summary>
        /// Выбирает точку врезания: центр области, если он внутри неё,
        /// иначе — середина длиннейшего сегмента скан-линии области.
        /// </summary>
        /// <param name="geometry">Геометрия области.</param>
        /// <param name="contourOffset">Отступ траектории от стенки.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок.</param>
        /// <param name="contourPoints">Точки контура области (для скан-линий).</param>
        /// <param name="center">Центр области — кандидат по умолчанию.</param>
        /// <param name="step">Шаг обработки: он же шаг скан-линий.</param>
        /// <param name="requiredClearance">Требуемый радиальный запас до
        /// контура; ноль сохраняет выбор точки для вертикального входа.</param>
        public static (double x, double y) Choose(
            IPocketGeometry geometry,
            double contourOffset,
            double taperOffset,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            double step,
            double requiredClearance = 0.0)
        {
            var centerIsInside = geometry.IsPointInside(center.x, center.y, contourOffset, taperOffset);
            if (centerIsInside
                && (requiredClearance <= 0
                    || ClearanceToContour(center, contourPoints) >= requiredClearance))
                return center;

            var lines = PocketScanLines.Build(contourPoints, center, 0.0, step);
            var bestLength = 0.0;
            var best = center;
            var bestClearance = centerIsInside ? ClearanceToContour(center, contourPoints) : 0.0;
            foreach (var line in lines)
            {
                foreach (var segment in line.Segments)
                {
                    var length = segment.x2 - segment.x1;
                    var candidate = PocketScanLines.ToWorld(
                        ((segment.x1 + segment.x2) / 2.0, line.Y), center, 0.0);

                    // Вертикальному входу сохраняем прежнее правило — середина
                    // самого длинного сегмента. Винтовому нужен не самый
                    // длинный разрез по X, а наибольший круговой запас до
                    // любой стороны области.
                    if (requiredClearance <= 0 && length > bestLength)
                    {
                        bestLength = length;
                        best = candidate;
                    }
                    else if (requiredClearance > 0)
                    {
                        var clearance = ClearanceToContour(candidate, contourPoints);
                        if (clearance > bestClearance)
                        {
                            bestClearance = clearance;
                            best = candidate;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Наименьшее расстояние от точки до замкнутого контура. Это радиус
        /// наибольшей окружности с данным центром, которая целиком остаётся
        /// внутри области (при условии, что центр внутри).
        /// </summary>
        public static double ClearanceToContour(
            (double x, double y) point,
            IReadOnlyList<(double x, double y)> contourPoints)
        {
            if (contourPoints == null || contourPoints.Count < 2)
                return 0.0;

            var clearance = double.MaxValue;
            for (int i = 0; i < contourPoints.Count; i++)
            {
                var start = contourPoints[i];
                var end = contourPoints[(i + 1) % contourPoints.Count];
                var distance = Geometry2D.DistanceToSegment(
                    point.x, point.y,
                    start.x, start.y,
                    end.x, end.y,
                    GeometryTolerances.Degenerate);
                if (distance < clearance)
                    clearance = distance;
            }

            return clearance == double.MaxValue ? 0.0 : clearance;
        }
    }
}
