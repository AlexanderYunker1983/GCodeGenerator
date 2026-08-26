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
        public void Generate(
            OperationBase operation,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            if (!(operation is DrillPointsOperation drill))
                return;

            int holeIndex = 0;
            foreach (var hole in drill.HolesToDrill)
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

                    builder.RapidTo(z: currentZ, feed: hole.FeedZRapid);
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
