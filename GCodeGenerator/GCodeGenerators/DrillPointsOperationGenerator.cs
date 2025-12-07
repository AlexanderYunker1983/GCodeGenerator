using System;
using System.Globalization;
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
            var culture = CultureInfo.InvariantCulture;

            foreach (var hole in drill.Holes)
            {
                addLine($"{g0} Z{drill.SafeZBetweenHoles.ToString(fmt, culture)} F{hole.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{hole.X.ToString(fmt, culture)} Y{hole.Y.ToString(fmt, culture)} F{drill.FeedXYRapid.ToString(fmt, culture)}");

                var currentZ = hole.Z;
                var finalZ = hole.Z - hole.TotalDepth;

                while (currentZ > finalZ)
                {
                    var nextZ = currentZ - hole.StepDepth;
                    if (nextZ < finalZ)
                        nextZ = finalZ;

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{hole.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{hole.FeedZWork.ToString(fmt, culture)}");

                    currentZ = nextZ;

                    if (currentZ > finalZ)
                        addLine($"{g0} Z{hole.RetractHeight.ToString(fmt, culture)} F{hole.FeedZRapid.ToString(fmt, culture)}");
                }

                addLine($"{g0} Z{drill.SafeZBetweenHoles.ToString(fmt, culture)} F{hole.FeedZRapid.ToString(fmt, culture)}");
            }
        }
    }
}

