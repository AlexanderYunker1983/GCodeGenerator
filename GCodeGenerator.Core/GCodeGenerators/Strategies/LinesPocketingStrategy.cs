using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Линейная (Lines) стратегия обработки кармана (пункт 5.5 плана).
    /// Параллельные проходы под углом <c>op.LineAngleDeg</c>: скан-линии стоят
    /// в серединах равных полос высотой ≤ step (<see cref="PocketScanLines"/>),
    /// каждый сегмент сечения — независимый рез с отводами:
    /// подъём на SafeZ → быстрый подход к началу сегмента → вход на рабочую Z
    /// (<c>workingZ</c>) → рез G1 до конца сегмента.
    ///
    /// Рез всегда слева направо в локальных координатах (без серпантина);
    /// острова и разрывы обрабатываются естественно — каждый сегмент
    /// сечения скан-линией становится отдельным проходом.
    ///
    /// Направление фрезерования (climb/conventional) для параллельных
    /// проходов не определяется (аналогично <see cref="ZigZagPocketingStrategy"/>).
    ///
    /// Допущение: выпуклый контур (тот же класс допущений, что у legacy-спирали):
    /// связочное перемещение генератора «в центр» после последнего прохода
    /// проходит по дну слоя внутри контура.
    /// </summary>
    public sealed class LinesPocketingStrategy : IPocketPocketingStrategy
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
            int decimals = op.Decimals;

            if (contourPoints == null || contourPoints.Count < 3 || step <= 0)
                return;

            var scanLines = PocketScanLines.Build(contourPoints, center, op.LineAngleDeg, step);

            foreach (var line in scanLines)
            {
                foreach (var seg in line.Segments)
                {
                    var entry = PocketScanLines.ToWorld((seg.x1, line.Y), center, op.LineAngleDeg);
                    var exit = PocketScanLines.ToWorld((seg.x2, line.Y), center, op.LineAngleDeg);

                    // Независимый рез: подъём → подход → вход в слой → рез
                    builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);
                    builder.RapidTo(x: entry.x, y: entry.y, feed: op.FeedXYRapid, decimals: decimals);
                    builder.RapidTo(z: workingZ, feed: op.FeedZRapid, decimals: decimals);
                    builder.LinearTo(x: exit.x, y: exit.y, feed: op.FeedXYWork, decimals: decimals);
                }
            }
        }
    }
}
