#nullable enable

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
        public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
        {
            var op = layer.Operation;
            // Стратегия работает на рабочей Z без отводов — workingZ не используется.
            if (layer.ContourPoints == null || layer.ContourPoints.Count < 3 || layer.Step <= 0)
                return;

            var scanLines = PocketScanLines.Build(layer.ContourPoints, layer.Center, op.LineAngleDeg, layer.Step);
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

                    var from = PocketScanLines.ToWorld((xFrom, line.Y), layer.Center, op.LineAngleDeg);
                    var to = PocketScanLines.ToWorld((xTo, line.Y), layer.Center, op.LineAngleDeg);

                    // Связка к ближнему концу сегмента (первый раз — из центра,
                    // дальше — от конца предыдущего реза: у соседних линий это
                    // короткий шаг вдоль стенки), затем рез вдоль скан-линии.
                    // Прежде выводился только дальний конец: рез «x1 → x2»,
                    // обещанный шапкой, не выполнялся никогда, траектория
                    // вырождалась в диагонали через весь карман, а у стенок
                    // шаг между реально пройденными путями удваивался.
                    builder.LinearTo(x: from.x, y: from.y, feed: op.FeedXYWork);
                    builder.LinearTo(x: to.x, y: to.y, feed: op.FeedXYWork);
                }
            }
        }
    }
}
