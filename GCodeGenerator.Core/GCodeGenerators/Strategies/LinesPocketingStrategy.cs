#nullable enable

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Линейная (Lines) стратегия обработки кармана (пункт 5.5 плана).
    /// Параллельные проходы под углом <c>op.LineAngleDeg</c>: скан-линии стоят
    /// в серединах равных полос высотой ≤ step (<see cref="PocketScanLines"/>),
    /// каждый сегмент сечения — независимый рез с отводами:
    /// подъём на SafeZ → быстрый подход к началу сегмента → врезание на рабочую Z
    /// (<see cref="PocketLayerEntry"/>) → рез G1 до конца сегмента.
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
        public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
        {
            var op = layer.Operation;
            if (layer.ContourPoints == null || layer.ContourPoints.Count < 3 || layer.Step <= 0)
                return;

            var scanLines = PocketScanLines.Build(layer.BoundaryContours, layer.Center, op.LineAngleDeg, layer.Step);

            foreach (var line in scanLines)
            {
                foreach (var seg in line.Segments)
                {
                    var entry = PocketScanLines.ToWorld((seg.x1, line.Y), layer.Center, op.LineAngleDeg);
                    var exit = PocketScanLines.ToWorld((seg.x2, line.Y), layer.Center, op.LineAngleDeg);

                    // Независимый рез: подъём → подход → вход в слой → рез
                    PocketLayerEntry.Enter(layer, builder, entry.x, entry.y);
                    builder.LinearTo(x: exit.x, y: exit.y, feed: op.FeedXYWork);
                }
            }
        }
    }
}
