using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Зигзаг (серпантин) стратегия обработки кармана (пункт 5.4 плана).
    /// Чёрпковые проходы под углом <c>op.LineAngleDeg</c>: скан-линии стоят
    /// в серединах равных полос высотой ≤ step (<see cref="PocketScanLines"/>),
    /// направление резания чередуется на каждой линии (чётные — слева направо,
    /// нечётные — справа налево), связки между сегментами и линиями —
    /// прямые G1 на рабочей подаче (без отводов).
    ///
    /// Стратегия работает на рабочей Z без отводов — <c>workingZ</c> не используется.
    ///
    /// Направление фрезерования (climb/conventional) для серпантинных
    /// проходов не определяется (аналогично <see cref="RadialPocketingStrategy"/>);
    /// первая линия всегда фрезеруется слева направо в локальных координатах.
    ///
    /// Допущение: выпуклый контур (тот же класс допущений, что у legacy-спирали
    /// по центру внутри контура): связочные перемещения между сегментами
    /// одной скан-линии могут проходить над воздухом при наличии островов.
    /// </summary>
    public sealed class ZigZagPocketingStrategy : IPocketPocketingStrategy
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
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            // Стратегия работает на рабочей Z без отводов — workingZ не используется.
            int decimals = op.Decimals;

            if (contourPoints == null || contourPoints.Count < 3 || step <= 0)
                return;

            var scanLines = PocketScanLines.Build(contourPoints, center, op.LineAngleDeg, step);
            if (scanLines.Count == 0)
                return;

            for (int k = 0; k < scanLines.Count; k++)
            {
                var line = scanLines[k];
                bool leftToRight = (k % 2) == 0;

                for (int s = 0; s < line.Segments.Count; s++)
                {
                    // Чётная линия: сегменты по порядку, рез x1 → x2.
                    // Нечётная: порядок сегментов развёрнут, рез x2 → x1.
                    var seg = leftToRight
                        ? line.Segments[s]
                        : line.Segments[line.Segments.Count - 1 - s];

                    double xFrom = leftToRight ? seg.x1 : seg.x2;
                    double xTo = leftToRight ? seg.x2 : seg.x1;

                    var to = PocketScanLines.ToWorld((xTo, line.Y), center, op.LineAngleDeg);

                    // Первый вызов — связка из центра (инструмент уже на рабочей Z);
                    // остальные — рез сегмента или связка к следующему сегменту.
                    builder.LinearTo(x: to.x, y: to.y, feed: op.FeedXYWork, decimals: decimals);
                }
            }
        }
    }
}
