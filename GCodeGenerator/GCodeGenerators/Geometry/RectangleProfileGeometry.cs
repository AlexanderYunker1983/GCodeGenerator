using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для прямоугольного профиля.
    /// </summary>
    public class RectangleProfileGeometry : IProfileGeometry
    {
        private readonly ProfileRectangleOperation _operation;

        public RectangleProfileGeometry(ProfileRectangleOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false;

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

            var corners = new[]
            {
                (-halfWidth, -halfHeight),
                (halfWidth, -halfHeight),
                (halfWidth, halfHeight),
                (-halfWidth, halfHeight)
            };

            var angleRad = _operation.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            var rotatedCorners = corners.Select(c =>
            {
                var x = c.Item1 * cos - c.Item2 * sin;
                var y = c.Item1 * sin + c.Item2 * cos;
                return (centerX + x, centerY + y);
            }).ToArray();

            int[] cornerOrder;
            if (direction == MillingDirection.Clockwise)
            {
                cornerOrder = new[] { 0, 3, 2, 1 };
            }
            else
            {
                cornerOrder = new[] { 0, 1, 2, 3 };
            }

            foreach (var idx in cornerOrder)
            {
                yield return rotatedCorners[idx];
            }

            // Замыкаем контур
            yield return rotatedCorners[cornerOrder[0]];
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

            // Периметр = 2*(ширина + высота), где ширина = 2*halfWidth, высота = 2*halfHeight
            return 2.0 * (2.0 * halfWidth + 2.0 * halfHeight);
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // Прямоугольник не имеет дуг
        }
    }
}

