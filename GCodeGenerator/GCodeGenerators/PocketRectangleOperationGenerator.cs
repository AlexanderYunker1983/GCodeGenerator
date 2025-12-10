using System;
using System.Globalization;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketRectangleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            var op = operation as PocketRectangleOperation;
            if (op == null) return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var step = op.ToolDiameter * (op.StepPercentOfTool <= 0 ? 0.4 : op.StepPercentOfTool / 100.0);
            if (step <= 0.001) step = op.ToolDiameter * 0.4;

            // center from reference point
            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY, op.Width, op.Height, out var cx, out var cy);

            var halfW = op.Width / 2.0 - toolRadius;
            var halfH = op.Height / 2.0 - toolRadius;
            if (halfW <= 0 || halfH <= 0) return;

            var angleRad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            (double X, double Y) Rot(double x, double y) => (cx + x * cos - y * sin, cy + x * sin + y * cos);

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

                // start at center
                addLine($"{g0} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                var minHalf = Math.Min(halfW, halfH);
                var offsets = new System.Collections.Generic.List<double>();
                var maxOffset = minHalf - 1e-6;
                for (double o = 0; o <= maxOffset; o += step)
                    offsets.Add(o);
                if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxOffset)
                    offsets.Add(maxOffset);
                offsets = offsets.OrderByDescending(v => v).ToList(); // from inner (small w/h) to outer

                var clockwise = op.Direction == MillingDirection.Clockwise;
                (double X, double Y) lastPoint = (cx, cy);

                foreach (var offset in offsets)
                {
                    var w = halfW - offset;
                    var h = halfH - offset;
                    if (w <= 0 || h <= 0) break;

                    var rect = new[]
                    {
                        Rot(-w, -h),
                        Rot(w, -h),
                        Rot(w, h),
                        Rot(-w, h),
                        Rot(-w, -h)
                    };

                    if (clockwise)
                        rect = new[] { rect[0], rect[3], rect[2], rect[1], rect[0] };

                    // connect from last point to start of this loop with cutting move
                    addLine($"{g1} X{rect[0].X.ToString(fmt, culture)} Y{rect[0].Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    lastPoint = rect[0];

                    for (int i = 1; i < rect.Length; i++)
                    {
                        var p = rect[i];
                        addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                        lastPoint = p;
                    }
                }

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                currentZ = nextZ;
            }
        }

        private void GetCenter(ReferencePointType type, double refX, double refY, double width, double height, out double cx, out double cy)
        {
            switch (type)
            {
                case ReferencePointType.Center:
                    cx = refX; cy = refY; break;
                case ReferencePointType.TopLeft:
                    cx = refX + width / 2.0; cy = refY - height / 2.0; break;
                case ReferencePointType.TopRight:
                    cx = refX - width / 2.0; cy = refY - height / 2.0; break;
                case ReferencePointType.BottomLeft:
                    cx = refX + width / 2.0; cy = refY + height / 2.0; break;
                case ReferencePointType.BottomRight:
                    cx = refX - width / 2.0; cy = refY + height / 2.0; break;
                default:
                    cx = refX; cy = refY; break;
            }
        }
    }
}


