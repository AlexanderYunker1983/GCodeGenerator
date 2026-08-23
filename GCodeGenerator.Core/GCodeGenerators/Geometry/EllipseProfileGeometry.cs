using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для эллиптического профиля.
    /// </summary>
    public class EllipseProfileGeometry : IProfileGeometry
    {
        private readonly ProfileEllipseOperation _operation;

        public EllipseProfileGeometry(ProfileEllipseOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => false; // Эллипс не может быть представлен как дуга в G-коде

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            var actualRadiusX = _operation.RadiusX + toolOffset;
            var actualRadiusY = _operation.RadiusY + toolOffset;

            var h = Math.Pow(actualRadiusX - actualRadiusY, 2) / Math.Pow(actualRadiusX + actualRadiusY, 2);
            var ellipsePerimeter = Math.PI * (actualRadiusX + actualRadiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            var numSegments = Math.Max(8, (int)Math.Ceiling(ellipsePerimeter / _operation.MaxSegmentLength));
            var angleStep = 2 * Math.PI / numSegments;

            if (direction == MillingDirection.Clockwise)
                angleStep = -angleStep;

            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;

            for (int i = 0; i <= numSegments; i++)
            {
                var angle = i * angleStep;
                var x_ellipse = actualRadiusX * Math.Cos(angle);
                var y_ellipse = actualRadiusY * Math.Sin(angle);
                var x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
                var y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
                var x = _operation.CenterX + x_rotated;
                var y = _operation.CenterY + y_rotated;
                yield return (x, y);
            }
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            var actualRadiusX = _operation.RadiusX + toolOffset;
            var actualRadiusY = _operation.RadiusY + toolOffset;
            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var startAngle = 0.0;
            var x_ellipse = actualRadiusX * Math.Cos(startAngle);
            var y_ellipse = actualRadiusY * Math.Sin(startAngle);
            var x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
            var y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
            return (_operation.CenterX + x_rotated, _operation.CenterY + y_rotated);
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var actualRadiusX = _operation.RadiusX + toolOffset;
            var actualRadiusY = _operation.RadiusY + toolOffset;

            var h = Math.Pow(actualRadiusX - actualRadiusY, 2) / Math.Pow(actualRadiusX + actualRadiusY, 2);
            var perimeter = Math.PI * (actualRadiusX + actualRadiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            var angle = (distance / perimeter) * 2 * Math.PI;

            if (_operation.Direction == MillingDirection.Clockwise)
                angle = -angle;

            var rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            var x_ellipse = actualRadiusX * Math.Cos(angle);
            var y_ellipse = actualRadiusY * Math.Sin(angle);
            var x_rotated = x_ellipse * Math.Cos(rotationRad) - y_ellipse * Math.Sin(rotationRad);
            var y_rotated = x_ellipse * Math.Sin(rotationRad) + y_ellipse * Math.Cos(rotationRad);
            return (_operation.CenterX + x_rotated, _operation.CenterY + y_rotated);
        }

        public double GetPerimeter(double toolOffset)
        {
            var actualRadiusX = _operation.RadiusX + toolOffset;
            var actualRadiusY = _operation.RadiusY + toolOffset;

            // Формула Рамануджана для периметра эллипса
            var h = Math.Pow(actualRadiusX - actualRadiusY, 2) / Math.Pow(actualRadiusX + actualRadiusY, 2);
            return Math.PI * (actualRadiusX + actualRadiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            yield break; // Эллипс не может быть представлен как дуга в G-коде
        }
    }
}

