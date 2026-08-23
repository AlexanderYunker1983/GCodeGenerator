using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для прямоугольного профиля со скругленными углами.
    /// </summary>
    public class RoundedRectangleProfileGeometry : IProfileGeometry
    {
        private readonly ProfileRoundedRectangleOperation _operation;

        public RoundedRectangleProfileGeometry(ProfileRoundedRectangleOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => true;

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            double centerX, centerY;
            switch (_operation.ReferencePointType)
            {
                case ReferencePointType.Center:
                    centerX = _operation.ReferencePointX;
                    centerY = _operation.ReferencePointY;
                    break;
                case ReferencePointType.TopLeft:
                    centerX = _operation.ReferencePointX + _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY - _operation.Height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    centerX = _operation.ReferencePointX - _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY - _operation.Height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    centerX = _operation.ReferencePointX + _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY + _operation.Height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    centerX = _operation.ReferencePointX - _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY + _operation.Height / 2.0;
                    break;
                default:
                    centerX = _operation.ReferencePointX;
                    centerY = _operation.ReferencePointY;
                    break;
            }

            var halfWidth = _operation.Width / 2.0 + toolOffset;
            var halfHeight = _operation.Height / 2.0 + toolOffset;

            double ClampRadius(double r) => Math.Max(0, r + toolOffset);
            var radii = new[]
            {
                ClampRadius(_operation.RadiusTopLeft),
                ClampRadius(_operation.RadiusTopRight),
                ClampRadius(_operation.RadiusBottomRight),
                ClampRadius(_operation.RadiusBottomLeft)
            };

            var maxRadiusX = halfWidth;
            var maxRadiusY = halfHeight;
            for (int i = 0; i < radii.Length; i++)
            {
                radii[i] = Math.Min(radii[i], Math.Min(maxRadiusX, maxRadiusY));
            }

            var angleRad = _operation.RotationAngle * Math.PI / 180.0;
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

            int[] order = direction == MillingDirection.Clockwise
                ? new[] { 0, 3, 2, 1 }
                : new[] { 0, 1, 2, 3 };

            var pathSegments = order.Zip(order.Skip(1).Concat(new[] { order[0] }), (startIdx, endIdx) => new { startIdx, endIdx }).ToArray();

            (double X, double Y) GetArcCenter(int cornerIndex, double radius)
            {
                switch (cornerIndex)
                {
                    case 0: return (-halfWidth + radius, -halfHeight + radius);
                    case 1: return (halfWidth - radius, -halfHeight + radius);
                    case 2: return (halfWidth - radius, halfHeight - radius);
                    case 3: return (-halfWidth + radius, halfHeight - radius);
                    default: return (0, 0);
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

            // Генерируем точки контура
            foreach (var segment in points)
            {
                var lineStart = RotateAndShift(segment.LineStart.X, segment.LineStart.Y);
                yield return lineStart;

                var arcRadius = radii[segment.LineEnd.CornerIndex];
                if (arcRadius > 0)
                {
                    var cornerIdx = segment.LineEnd.CornerIndex;
                    var corner = GetArcCenter(cornerIdx, arcRadius);
                    var nextSegment = points[(Array.IndexOf(points, segment) + 1) % points.Length];

                    var startVec = Normalize(subtract(segment.LineEnd.X, segment.LineEnd.Y, corner.Item1, corner.Item2));
                    var endVec = Normalize(subtract(nextSegment.LineStart.X, nextSegment.LineStart.Y, corner.Item1, corner.Item2));

                    var angleStart = Math.Atan2(startVec.Y, startVec.X);
                    var angleEnd = Math.Atan2(endVec.Y, endVec.X);
                    var sweep = NormalizeAngle(angleEnd - angleStart);
                    if (direction == MillingDirection.Clockwise && sweep > 0) sweep -= 2 * Math.PI;
                    if (direction == MillingDirection.CounterClockwise && sweep < 0) sweep += 2 * Math.PI;

                    var arcLength = Math.Abs(sweep) * arcRadius;
                    var maxSegLen = Math.Max(0.001, _operation.MaxSegmentLength);
                    var steps = Math.Max(1, (int)Math.Ceiling(arcLength / maxSegLen));
                    var stepSweep = sweep / steps;

                    for (int s = 1; s <= steps; s++)
                    {
                        var ang = angleStart + stepSweep * s;
                        var ax = corner.Item1 + arcRadius * Math.Cos(ang);
                        var ay = corner.Item2 + arcRadius * Math.Sin(ang);
                        var rot = RotateAndShift(ax, ay);
                        yield return rot;
                    }
                }
                else
                {
                    var lineEnd = RotateAndShift(segment.LineEnd.X, segment.LineEnd.Y);
                    yield return lineEnd;
                }
            }
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            return GetContourPoints(toolOffset, _operation.Direction).First();
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var points = GetContourPoints(toolOffset, _operation.Direction).ToList();
            var perimeter = GetPerimeter(toolOffset);
            var normalizedDistance = distance % perimeter;
            if (normalizedDistance < 0) normalizedDistance += perimeter;

            double accumulated = 0.0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                var segmentLength = Math.Sqrt(Math.Pow(p2.x - p1.x, 2) + Math.Pow(p2.y - p1.y, 2));

                if (accumulated + segmentLength >= normalizedDistance)
                {
                    var t = (normalizedDistance - accumulated) / segmentLength;
                    return (p1.x + t * (p2.x - p1.x), p1.y + t * (p2.y - p1.y));
                }

                accumulated += segmentLength;
            }

            return points[0];
        }

        public double GetPerimeter(double toolOffset)
        {
            double centerX, centerY;
            switch (_operation.ReferencePointType)
            {
                case ReferencePointType.Center:
                    centerX = _operation.ReferencePointX;
                    centerY = _operation.ReferencePointY;
                    break;
                case ReferencePointType.TopLeft:
                    centerX = _operation.ReferencePointX + _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY - _operation.Height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    centerX = _operation.ReferencePointX - _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY - _operation.Height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    centerX = _operation.ReferencePointX + _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY + _operation.Height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    centerX = _operation.ReferencePointX - _operation.Width / 2.0;
                    centerY = _operation.ReferencePointY + _operation.Height / 2.0;
                    break;
                default:
                    centerX = _operation.ReferencePointX;
                    centerY = _operation.ReferencePointY;
                    break;
            }

            var halfWidth = _operation.Width / 2.0 + toolOffset;
            var halfHeight = _operation.Height / 2.0 + toolOffset;

            double ClampRadius(double r) => Math.Max(0, r + toolOffset);
            var radii = new[]
            {
                ClampRadius(_operation.RadiusTopLeft),
                ClampRadius(_operation.RadiusTopRight),
                ClampRadius(_operation.RadiusBottomRight),
                ClampRadius(_operation.RadiusBottomLeft)
            };

            var maxRadiusX = halfWidth;
            var maxRadiusY = halfHeight;
            for (int i = 0; i < radii.Length; i++)
            {
                radii[i] = Math.Min(radii[i], Math.Min(maxRadiusX, maxRadiusY));
            }

            var corners = new[]
            {
                (-halfWidth, -halfHeight),
                (halfWidth, -halfHeight),
                (halfWidth, halfHeight),
                (-halfWidth, halfHeight)
            };

            int[] order = _operation.Direction == MillingDirection.Clockwise
                ? new[] { 0, 3, 2, 1 }
                : new[] { 0, 1, 2, 3 };

            var pathSegments = order.Zip(order.Skip(1).Concat(new[] { order[0] }), (startIdx, endIdx) => new { startIdx, endIdx }).ToArray();

            var perimeter = 0.0;
            foreach (var seg in pathSegments)
            {
                var startCorner = corners[seg.startIdx];
                var endCorner = corners[seg.endIdx];
                var dx = endCorner.Item1 - startCorner.Item1;
                var dy = endCorner.Item2 - startCorner.Item2;
                var len = Math.Sqrt(dx * dx + dy * dy);
                perimeter += len;
                var r = radii[seg.endIdx];
                if (r > 0)
                    perimeter += Math.PI * r / 2.0; // четверть окружности
            }

            return perimeter;
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            // Реализация для получения сегментов дуг - сложная логика, пока возвращаем пустую коллекцию
            // Можно реализовать позже при необходимости
            yield break;
        }

        private (double X, double Y) subtract(double x1, double y1, double x2, double y2) => (x1 - x2, y1 - y2);

        private (double X, double Y) Normalize((double X, double Y) v)
        {
            var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len < 1e-9) return (0, 0);
            return (v.X / len, v.Y / len);
        }

        private double NormalizeAngle(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}

