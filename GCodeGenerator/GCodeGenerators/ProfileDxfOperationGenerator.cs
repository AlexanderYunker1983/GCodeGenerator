using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfileDxfOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            var op = operation as ProfileDxfOperation;
            if (op == null)
                return;

            var fmt = $"0.{new string('0', Math.Max(0, op.Decimals))}";
            var culture = CultureInfo.InvariantCulture;

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var passNumber = 0;

            if (op.Polylines == null || op.Polylines.Count == 0)
            {
                addLine("(DXF operation has no lines)");
                return;
            }

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ)
                    nextZ = finalZ;

                passNumber++;
                if (settings.UseComments)
                    addLine($"(Pass {passNumber}, depth {nextZ.ToString(fmt, culture)})");

                foreach (var poly in op.Polylines)
                {
                    if (poly?.Points == null || poly.Points.Count < 2)
                        continue;

                    var start = poly.Points[0];

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{start.X.ToString(fmt, culture)} Y{start.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    for (int i = 1; i < poly.Points.Count; i++)
                    {
                        var p = poly.Points[i];
                        addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                if (nextZ > finalZ)
                {
                    var retractZAfterPass = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{retractZAfterPass.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                currentZ = nextZ;
            }

            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }
    }
}


