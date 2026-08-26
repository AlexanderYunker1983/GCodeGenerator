#nullable enable
using System.Collections.Generic;
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
        public static (double x, double y) Choose(
            IPocketGeometry geometry,
            double contourOffset,
            double taperOffset,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            double step)
        {
            if (geometry.IsPointInside(center.x, center.y, contourOffset, taperOffset))
                return center;

            var lines = PocketScanLines.Build(contourPoints, center, 0.0, step);
            var bestLength = 0.0;
            var best = center;
            foreach (var line in lines)
            {
                foreach (var segment in line.Segments)
                {
                    var length = segment.x2 - segment.x1;
                    if (length > bestLength)
                    {
                        bestLength = length;
                        best = PocketScanLines.ToWorld(((segment.x1 + segment.x2) / 2.0, line.Y), center, 0.0);
                    }
                }
            }

            return best;
        }
    }
}
