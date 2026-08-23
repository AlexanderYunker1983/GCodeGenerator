using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class DrillPointsOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, ProgramBuilder builder, GCodeSettings settings)
        {
            if (!(operation is DrillPointsOperation drill))
                return;

            int decimals = drill.Decimals;

            int holeIndex = 0;
            foreach (var hole in drill.Holes)
            {
                // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл сверления
                // превращается в бесконечный. Бросаем исключение вместо зависания.
                if (hole.StepDepth <= 0)
                    throw new ArgumentOutOfRangeException(nameof(drill),
                        $"StepDepth of hole {holeIndex + 1} must be greater than zero (got {hole.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the drilling loop would run forever.");

                builder.RapidTo(z: drill.SafeZBetweenHoles, feed: hole.FeedZRapid, decimals: decimals);
                builder.RapidTo(x: hole.X, y: hole.Y, feed: drill.FeedXYRapid, decimals: decimals);

                var currentZ = hole.Z;
                var finalZ = hole.Z - hole.TotalDepth;

                while (currentZ > finalZ)
                {
                    var nextZ = currentZ - hole.StepDepth;
                    if (nextZ < finalZ)
                        nextZ = finalZ;

                    builder.RapidTo(z: currentZ, feed: hole.FeedZRapid, decimals: decimals);
                    builder.LinearTo(z: nextZ, feed: hole.FeedZWork, decimals: decimals);

                    currentZ = nextZ;

                    if (currentZ > finalZ)
                        builder.RapidTo(z: hole.RetractHeight, feed: hole.FeedZRapid, decimals: decimals);
                }

                builder.RapidTo(z: drill.SafeZBetweenHoles, feed: hole.FeedZRapid, decimals: decimals);

                holeIndex++;
            }
        }
    }
}
