using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для многоугольного профиля.
    /// </summary>
    public class PolygonProfileGeometry : IProfileGeometry
    {
        private readonly ProfilePolygonOperation _operation;

        public PolygonProfileGeometry(ProfilePolygonOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false;

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            var actualRadius = _operation.Radius + toolOffset;
            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var angleStep = 2 * Math.PI / _operation.NumberOfSides;

            var vertices = new List<(double x, double y)>();
            for (int i = 0; i < _operation.NumberOfSides; i++)
            {
                var angle = i * angleStep + rotationRad;
                var x = _operation.CenterX + actualRadius * Math.Cos(angle);
                var y = _operation.CenterY + actualRadius * Math.Sin(angle);
                vertices.Add((x, y));
            }

            if (direction == MillingDirection.Clockwise)
            {
                for (int i = _operation.NumberOfSides - 1; i >= 0; i--)
                {
                    yield return vertices[i];
                }
            }
            else
            {
                foreach (var vertex in vertices)
                {
                    yield return vertex;
                }
            }

            // Замыкаем контур
            yield return vertices[0];
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            var actualRadius = _operation.Radius + toolOffset;
            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var angle = rotationRad;
            return (_operation.CenterX + actualRadius * Math.Cos(angle), _operation.CenterY + actualRadius * Math.Sin(angle));
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
            var actualRadius = _operation.Radius + toolOffset;
            var angleStep = 2 * Math.PI / _operation.NumberOfSides;
            var sideLength = 2 * actualRadius * Math.Sin(angleStep / 2.0);
            return _operation.NumberOfSides * sideLength;
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // Многоугольник не имеет дуг
        }
    }
}

