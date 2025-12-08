using System;
using System.Globalization;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class ProfileRoundedRectangleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation, Action<string> addLine, string g0, string g1, GCodeSettings settings)
        {
            var op = operation as ProfileRoundedRectangleOperation;
            if (op == null)
                return;

            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var toolRadius = op.ToolDiameter / 2.0;
            var offset = 0.0;
            if (op.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (op.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            double centerX, centerY;
            switch (op.ReferencePointType)
            {
                case ReferencePointType.Center:
                    centerX = op.ReferencePointX;
                    centerY = op.ReferencePointY;
                    break;
                case ReferencePointType.TopLeft:
                    centerX = op.ReferencePointX + op.Width / 2.0;
                    centerY = op.ReferencePointY - op.Height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    centerX = op.ReferencePointX - op.Width / 2.0;
                    centerY = op.ReferencePointY - op.Height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    centerX = op.ReferencePointX + op.Width / 2.0;
                    centerY = op.ReferencePointY + op.Height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    centerX = op.ReferencePointX - op.Width / 2.0;
                    centerY = op.ReferencePointY + op.Height / 2.0;
                    break;
                default:
                    centerX = op.ReferencePointX;
                    centerY = op.ReferencePointY;
                    break;
            }

            var halfWidth = op.Width / 2.0 + offset;
            var halfHeight = op.Height / 2.0 + offset;

            double ClampRadius(double r) { return Math.Max(0, r + offset); }

            var radii = new[]
            {
                ClampRadius(op.RadiusTopLeft),
                ClampRadius(op.RadiusTopRight),
                ClampRadius(op.RadiusBottomRight),
                ClampRadius(op.RadiusBottomLeft)
            };

            var maxRadiusX = halfWidth;
            var maxRadiusY = halfHeight;
            for (int i = 0; i < radii.Length; i++)
            {
                radii[i] = Math.Min(radii[i], Math.Min(maxRadiusX, maxRadiusY));
            }

            var angleRad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            (double X, double Y) RotateAndShift(double x, double y)
            {
                var rx = x * cos - y * sin;
                var ry = x * sin + y * cos;
                return (centerX + rx, centerY + ry);
            }

            var corners = new[]
            {
                (-halfWidth, -halfHeight),
                (halfWidth, -halfHeight),
                (halfWidth, halfHeight),
                (-halfWidth, halfHeight)
            };

            int[] order = op.Direction == MillingDirection.Clockwise
                ? new[] { 0, 3, 2, 1 }
                : new[] { 0, 1, 2, 3 };

            var pathSegments = order.Zip(order.Skip(1).Concat(new[] { order[0] }), (startIdx, endIdx) => new { startIdx, endIdx }).ToArray();

            (double X, double Y) GetArcCenter(int cornerIndex, double radius)
            {
                switch (cornerIndex)
                {
                    case 0:
                        return (-halfWidth + radius, -halfHeight + radius);
                    case 1:
                        return (halfWidth - radius, -halfHeight + radius);
                    case 2:
                        return (halfWidth - radius, halfHeight - radius);
                    case 3:
                        return (-halfWidth + radius, halfHeight - radius);
                    default:
                        return (0, 0);
                }
            }

            var points = pathSegments.Select(seg =>
            {
                var startIdx = seg.startIdx;
                var endIdx = seg.endIdx;
                var startCorner = corners[startIdx];
                var endCorner = corners[endIdx];
                var radiusAtStart = radii[startIdx];
                var radiusAtEnd = radii[endIdx];

                var dx = endCorner.Item1 - startCorner.Item1;
                var dy = endCorner.Item2 - startCorner.Item2;
                var len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) len = 1e-6;
                var ux = dx / len;
                var uy = dy / len;

                var p1x = startCorner.Item1 + ux * radiusAtStart;
                var p1y = startCorner.Item2 + uy * radiusAtStart;
                var p2x = endCorner.Item1 - ux * radiusAtEnd;
                var p2y = endCorner.Item2 - uy * radiusAtEnd;

                return new
                {
                    LineStart = (X: p1x, Y: p1y, R: radiusAtStart, CornerIndex: startIdx),
                    LineEnd = (X: p2x, Y: p2y, R: radiusAtEnd, CornerIndex: endIdx)
                };
            }).ToArray();

            var start = RotateAndShift(points[0].LineStart.X, points[0].LineStart.Y);

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var passNumber = 0;

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ)
                    nextZ = finalZ;

                passNumber++;

                if (settings.UseComments)
                    addLine($"(Pass {passNumber}, depth {nextZ.ToString(fmt, culture)})");

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{start.X.ToString(fmt, culture)} Y{start.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                if (op.EntryMode == EntryMode.Vertical)
                {
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");
                }
                else
                {
                    var entryAngleRad = op.EntryAngle * Math.PI / 180.0;
                    var retractZ = currentZ + op.RetractHeight;

                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    var rampDepth = retractZ - nextZ;
                    var rampDistance = rampDepth / Math.Tan(entryAngleRad);

                    // approximate perimeter
                    var perimeter = 0.0;
                    foreach (var segment in points)
                    {
                        var len = Math.Sqrt(Math.Pow(segment.LineEnd.X - segment.LineStart.X, 2) + Math.Pow(segment.LineEnd.Y - segment.LineStart.Y, 2));
                        perimeter += len;
                        var r = radii[segment.LineEnd.CornerIndex];
                        if (r > 0)
                            perimeter += Math.PI * r / 2.0; // quarter arc length
                    }

                    var rampFraction = Math.Min(1.0, rampDistance / Math.Max(1e-6, perimeter));
                    var rampLength = rampFraction * perimeter;

                    double accumulated = 0.0;
                    (double X, double Y, double Z) lastPoint = (start.X, start.Y, retractZ);
                    bool rampComplete = false;

                    foreach (var segment in points)
                    {
                        var lineLen = Math.Sqrt(Math.Pow(segment.LineEnd.X - segment.LineStart.X, 2) + Math.Pow(segment.LineEnd.Y - segment.LineStart.Y, 2));
                        var arcRadius = radii[segment.LineEnd.CornerIndex];
                        var arcLen = arcRadius > 0 ? Math.PI * arcRadius / 2.0 : 0.0;

                        void EmitPoint(double localProgress, double x, double y)
                        {
                            accumulated += localProgress;
                            var z = Math.Max(nextZ, retractZ - accumulated * Math.Tan(entryAngleRad));
                            var rotated = RotateAndShift(x, y);
                            addLine($"{g1} X{rotated.X.ToString(fmt, culture)} Y{rotated.Y.ToString(fmt, culture)} Z{z.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                            lastPoint = (rotated.X, rotated.Y, z);
                        }

                        if (accumulated >= rampLength)
                            break;

                        // line segment
                        if (lineLen > 1e-6)
                        {
                            var remaining = rampLength - accumulated;
                            if (remaining <= lineLen)
                            {
                                var t = remaining / lineLen;
                                var lx = segment.LineStart.X + (segment.LineEnd.X - segment.LineStart.X) * t;
                                var ly = segment.LineStart.Y + (segment.LineEnd.Y - segment.LineStart.Y) * t;
                                EmitPoint(remaining, lx, ly);
                                rampComplete = true;
                                break;
                            }
                            EmitPoint(lineLen, segment.LineEnd.X, segment.LineEnd.Y);
                        }

                        // arc segment approximated with linear steps to allow Z ramping
                        if (arcRadius > 0 && accumulated < rampLength)
                        {
                            var remaining = rampLength - accumulated;
                            var arcTravel = Math.Min(remaining, arcLen);

                            var cornerIdx = segment.LineEnd.CornerIndex;
                            var corner = GetArcCenter(cornerIdx, arcRadius);
                            var nextSegment = points[(Array.IndexOf(points, segment) + 1) % points.Length];

                            // Determine arc center and direction (start from the point where the line ended)
                            var prevDir = Normalize(subtract(segment.LineEnd.X, segment.LineEnd.Y, corner.Item1, corner.Item2));
                            var nextDir = Normalize(subtract(nextSegment.LineStart.X, nextSegment.LineStart.Y, corner.Item1, corner.Item2));

                            double angleStart = Math.Atan2(prevDir.Y, prevDir.X);
                            double angleEnd = Math.Atan2(nextDir.Y, nextDir.X);
                            var deltaAngle = NormalizeAngle(angleEnd - angleStart);
                            if (op.Direction == MillingDirection.Clockwise && deltaAngle > 0) deltaAngle -= 2 * Math.PI;
                            if (op.Direction == MillingDirection.CounterClockwise && deltaAngle < 0) deltaAngle += 2 * Math.PI;

                            var totalSweep = deltaAngle * (arcTravel / Math.Max(1e-9, arcLen));
                            var maxSegLen = Math.Max(0.001, op.MaxSegmentLength);
                            var steps = Math.Max(1, (int)Math.Ceiling(arcTravel / maxSegLen));
                            var stepLen = arcTravel / steps;

                            for (int s = 1; s <= steps; s++)
                            {
                                var frac = (double)s / steps;
                                var angle = angleStart + totalSweep * frac;
                                var ax = corner.Item1 + arcRadius * Math.Cos(angle);
                                var ay = corner.Item2 + arcRadius * Math.Sin(angle);

                                EmitPoint(stepLen, ax, ay);

                                if (accumulated >= rampLength)
                                {
                                    rampComplete = true;
                                    break;
                                }
                            }
                            if (rampComplete)
                                break;
                        }

                        if (rampComplete)
                            break;
                    }

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{start.X.ToString(fmt, culture)} Y{start.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                // Cut contour
                for (int i = 0; i < points.Length; i++)
                {
                    var segment = points[i];
                    var lineStart = RotateAndShift(segment.LineStart.X, segment.LineStart.Y);
                    var lineEnd = RotateAndShift(segment.LineEnd.X, segment.LineEnd.Y);

                    addLine($"{g1} X{lineEnd.X.ToString(fmt, culture)} Y{lineEnd.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    var arcRadius = radii[segment.LineEnd.CornerIndex];
                    if (arcRadius > 0 && settings.AllowArcs)
                    {
                        var cornerIdx = segment.LineEnd.CornerIndex;
                        var corner = GetArcCenter(cornerIdx, arcRadius);

                        var nextSegment = points[(i + 1) % points.Length];
                        var nextDir = Normalize(subtract(nextSegment.LineStart.X, nextSegment.LineStart.Y, corner.Item1, corner.Item2));

                        var arcEndX = corner.Item1 + nextDir.X * arcRadius;
                        var arcEndY = corner.Item2 + nextDir.Y * arcRadius;

                        var startRot = lineEnd;
                        var centerRot = RotateAndShift(corner.Item1, corner.Item2);
                        var arcEndRot = RotateAndShift(arcEndX, arcEndY);

                        var clockwise = op.Direction == MillingDirection.Clockwise;
                        var gArc = settings.UsePaddedGCodes ? (clockwise ? "G02" : "G03") : (clockwise ? "G2" : "G3");

                        var iVal = centerRot.X - startRot.X;
                        var jVal = centerRot.Y - startRot.Y;

                        addLine($"{gArc} X{arcEndRot.X.ToString(fmt, culture)} Y{arcEndRot.Y.ToString(fmt, culture)} I{iVal.ToString(fmt, culture)} J{jVal.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                    else if (arcRadius > 0 && !settings.AllowArcs)
                    {
                        // approximate quarter arc with configurable segment length
                        var cornerIdx = segment.LineEnd.CornerIndex;
                        var corner = GetArcCenter(cornerIdx, arcRadius);
                        var startVec = Normalize(subtract(segment.LineEnd.X, segment.LineEnd.Y, corner.Item1, corner.Item2));
                        var nextSegment = points[(i + 1) % points.Length];
                        var endVec = Normalize(subtract(nextSegment.LineStart.X, nextSegment.LineStart.Y, corner.Item1, corner.Item2));

                        var angleStart = Math.Atan2(startVec.Y, startVec.X);
                        var angleEnd = Math.Atan2(endVec.Y, endVec.X);
                        var sweep = NormalizeAngle(angleEnd - angleStart);
                        if (op.Direction == MillingDirection.Clockwise && sweep > 0) sweep -= 2 * Math.PI;
                        if (op.Direction == MillingDirection.CounterClockwise && sweep < 0) sweep += 2 * Math.PI;

                        var arcLength = Math.Abs(sweep) * arcRadius;
                        var maxSegLen = Math.Max(0.001, op.MaxSegmentLength);
                        var steps = Math.Max(1, (int)Math.Ceiling(arcLength / maxSegLen));
                        var stepSweep = sweep / steps;

                        for (int s = 1; s <= steps; s++)
                        {
                            var ang = angleStart + stepSweep * s;
                            var ax = corner.Item1 + arcRadius * Math.Cos(ang);
                            var ay = corner.Item2 + arcRadius * Math.Sin(ang);
                            var rot = RotateAndShift(ax, ay);
                            addLine($"{g1} X{rot.X.ToString(fmt, culture)} Y{rot.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                        }
                    }
                }

                // close contour
                addLine($"{g1} X{start.X.ToString(fmt, culture)} Y{start.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                if (nextZ > finalZ)
                {
                    var retractZAfterPass = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{retractZAfterPass.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                currentZ = nextZ;
            }

            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

            (double X, double Y) subtract(double x1, double y1, double x2, double y2) { return (x1 - x2, y1 - y2); }

            (double X, double Y) Normalize((double X, double Y) v)
            {
                var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
                if (len < 1e-9) return (0, 0);
                return (v.X / len, v.Y / len);
            }

            double NormalizeAngle(double a)
            {
                while (a > Math.PI) a -= 2 * Math.PI;
                while (a < -Math.PI) a += 2 * Math.PI;
                return a;
            }
        }
    }
}


