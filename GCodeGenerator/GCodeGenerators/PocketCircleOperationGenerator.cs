using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketCircleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            var op = operation as PocketCircleOperation;
            if (op == null) return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var step = op.ToolDiameter * (op.StepPercentOfTool <= 0 ? 0.4 : op.StepPercentOfTool / 100.0);
            if (step <= 0.001) step = op.ToolDiameter * 0.4;

            var effectiveRadius = op.Radius - toolRadius;
            if (effectiveRadius <= 0) return;

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var pass = 0;

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                pass++;

                if (settings.UseComments)
                    addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                var clockwise = op.Direction == MillingDirection.Clockwise;
                var offsets = new System.Collections.Generic.List<double>();
                for (double r = 0; r <= effectiveRadius; r += step)
                    offsets.Add(r);
                if (offsets.Count == 0 || offsets[offsets.Count - 1] < effectiveRadius)
                    offsets.Add(effectiveRadius);

                foreach (var offset in offsets)
                {
                    var r = offset;
                    var segments = Math.Max(32, (int)Math.Ceiling(2 * Math.PI * r / (op.ToolDiameter * 0.5)));
                    if (segments < 4) segments = 4;
                    var angleStep = 2 * Math.PI / segments * (clockwise ? -1 : 1);

                    double startAngle = 0;
                    var startX = op.CenterX + r * Math.Cos(startAngle);
                    var startY = op.CenterY + r * Math.Sin(startAngle);

                    addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (int i = 1; i <= segments; i++)
                    {
                        var ang = startAngle + angleStep * i;
                        var x = op.CenterX + r * Math.Cos(ang);
                        var y = op.CenterY + r * Math.Sin(ang);
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                }

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                currentZ = nextZ;
            }
        }
    }
}


