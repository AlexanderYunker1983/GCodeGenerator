using System;
using System.Globalization;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class DrillPointsOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            if (!(operation is DrillPointsOperation drill))
                return;

            var fmt = $"0.{new string('0', drill.Decimals)}";

            int holeIndex = 0;
            foreach (var hole in drill.Holes)
            {
                // Пункт 3.8 плана: StepDepth <= 0 не двигает Z вниз — цикл сверления
                // превращается в бесконечный. Бросаем исключение вместо зависания.
                if (hole.StepDepth <= 0)
                    throw new ArgumentOutOfRangeException(nameof(drill),
                        $"StepDepth of hole {holeIndex + 1} must be greater than zero (got {hole.StepDepth.ToString(CultureInfo.InvariantCulture)}); otherwise the drilling loop would run forever.");

                addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(drill.SafeZBetweenHoles, fmt)} F{GCodeGenerationHelper.FormatNumber(hole.FeedZRapid, fmt)}");
                addLine($"{g0} X{GCodeGenerationHelper.FormatNumber(hole.X, fmt)} Y{GCodeGenerationHelper.FormatNumber(hole.Y, fmt)} F{GCodeGenerationHelper.FormatNumber(drill.FeedXYRapid, fmt)}");

                var currentZ = hole.Z;
                var finalZ = hole.Z - hole.TotalDepth;

                while (currentZ > finalZ)
                {
                    var nextZ = currentZ - hole.StepDepth;
                    if (nextZ < finalZ)
                        nextZ = finalZ;

                    addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(currentZ, fmt)} F{GCodeGenerationHelper.FormatNumber(hole.FeedZRapid, fmt)}");
                    addLine($"{g1} Z{GCodeGenerationHelper.FormatNumber(nextZ, fmt)} F{GCodeGenerationHelper.FormatNumber(hole.FeedZWork, fmt)}");

                    currentZ = nextZ;

                    if (currentZ > finalZ)
                        addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(hole.RetractHeight, fmt)} F{GCodeGenerationHelper.FormatNumber(hole.FeedZRapid, fmt)}");
                }

                addLine($"{g0} Z{GCodeGenerationHelper.FormatNumber(drill.SafeZBetweenHoles, fmt)} F{GCodeGenerationHelper.FormatNumber(hole.FeedZRapid, fmt)}");

                holeIndex++;
            }
        }
    }
}

