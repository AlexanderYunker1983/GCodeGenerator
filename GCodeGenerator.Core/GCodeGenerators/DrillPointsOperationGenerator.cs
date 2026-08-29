#nullable enable
using System;
using System.Globalization;
using System.Threading;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    public class DrillPointsOperationGenerator : IOperationGenerator
    {
        /// <summary>
        /// Насколько выше пройденной глубины обрывается быстрый ход при
        /// возврате в отверстие, мм.
        ///
        /// Между проходами сверло выходит из отверстия — на высоту отвода,
        /// а она задана абсолютной, то есть чаще всего над поверхностью:
        /// проход выбрасывает стружку, и обратно сверло идёт по всей
        /// пройденной глубине. Прежде этот возврат шёл быстрым ходом до
        /// самого дна: оставшаяся в отверстии стружка встречала сверло на
        /// полной скорости, а встречает она его всегда — за тем отвод
        /// и делается.
        ///
        /// Полмиллиметра — зазор того же порядка, что и у постоянных циклов
        /// стойки, где он задаётся отдельным словом: достаточно, чтобы
        /// принять удар о стружку рабочей подачей, и слишком мало, чтобы
        /// заметно удлинить обработку.
        /// </summary>
        public const double PeckReturnClearance = 0.5;

        public void Generate(
            OperationBase operation,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            if (!(operation is DrillPointsOperation drill))
                return;

            int holeIndex = 0;
            foreach (var hole in drill.GetHolesToDrill(cancellation))
            {
                // Отверстие — единица работы: между отверстиями операцию
                // можно отменить, не дожидаясь конца шаблона.
                cancellation.ThrowIfCancellationRequested();

                // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл сверления
                // превращается в бесконечный. Бросаем исключение вместо зависания.
                if (hole.StepDepth <= 0)
                    throw new ArgumentOutOfRangeException(nameof(drill),
                        $"StepDepth of hole {holeIndex + 1} must be greater than zero (got {hole.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the drilling loop would run forever.");

                builder.RapidTo(z: drill.SafeZBetweenHoles, feed: hole.FeedZRapid);
                builder.RapidTo(x: hole.X, y: hole.Y, feed: drill.FeedXYRapid);

                var currentZ = hole.Z;
                var finalZ = hole.Z - hole.TotalDepth;

                while (currentZ > finalZ)
                {
                    var nextZ = currentZ - hole.StepDepth;
                    if (nextZ < finalZ)
                        nextZ = finalZ;

                    // Быстрый ход обрывается над пройденной глубиной, и
                    // последний участок сверло проходит рабочей подачей.
                    // Выше верха отверстия подниматься незачем: там материала
                    // нет, а на первом проходе сверло и так подводится к нему.
                    var entryZ = Math.Min(currentZ + PeckReturnClearance, hole.Z);

                    builder.RapidTo(z: entryZ, feed: hole.FeedZRapid);
                    builder.LinearTo(z: nextZ, feed: hole.FeedZWork);

                    currentZ = nextZ;

                    if (currentZ > finalZ)
                        builder.RapidTo(z: hole.RetractHeight, feed: hole.FeedZRapid);
                }

                builder.RapidTo(z: drill.SafeZBetweenHoles, feed: hole.FeedZRapid);

                holeIndex++;
            }
        }
    }
}
