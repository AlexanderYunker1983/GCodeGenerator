using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для круглого профиля.
    /// </summary>
    public class CircleProfileGeometry : IProfileGeometry
    {
        private readonly ProfileCircleOperation _operation;

        public CircleProfileGeometry(ProfileCircleOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public bool SupportsArcs => true;

        public IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction)
        {
            var actualRadius = _operation.Radius + toolOffset;
            var numSegments = Math.Max(8, (int)Math.Ceiling(2 * Math.PI * actualRadius / _operation.MaxSegmentLength));
            var angleStep = 2 * Math.PI / numSegments;

            if (direction == MillingDirection.Clockwise)
                angleStep = -angleStep;

            for (int i = 0; i <= numSegments; i++)
            {
                var angle = i * angleStep;
                var x = _operation.CenterX + actualRadius * Math.Cos(angle);
                var y = _operation.CenterY + actualRadius * Math.Sin(angle);
                yield return (x, y);
            }
        }

        public (double x, double y) GetStartPoint(double toolOffset)
        {
            var actualRadius = _operation.Radius + toolOffset;
            return (_operation.CenterX + actualRadius, _operation.CenterY);
        }

        public (double x, double y) GetPointOnContour(double distance, double toolOffset)
        {
            var actualRadius = _operation.Radius + toolOffset;
            var circumference = 2 * Math.PI * actualRadius;
            var angle = (distance / circumference) * 2 * Math.PI;

            if (_operation.Direction == MillingDirection.Clockwise)
                angle = -angle;

            var x = _operation.CenterX + actualRadius * Math.Cos(angle);
            var y = _operation.CenterY + actualRadius * Math.Sin(angle);
            return (x, y);
        }

        public double GetPerimeter(double toolOffset)
        {
            var actualRadius = _operation.Radius + toolOffset;
            return 2 * Math.PI * actualRadius;
        }

        public IEnumerable<IArcSegment> GetArcSegments(double toolOffset)
        {
            var actualRadius = _operation.Radius + toolOffset;
            var startPoint = GetStartPoint(toolOffset);
            var midPoint = (_operation.CenterX - actualRadius, _operation.CenterY);
            var endPoint = startPoint;

            yield return new CircleArcSegment
            {
                StartPoint = startPoint,
                EndPoint = midPoint,
                Center = (_operation.CenterX, _operation.CenterY),
                Radius = actualRadius,
                IsClockwise = _operation.Direction == MillingDirection.Clockwise
            };

            yield return new CircleArcSegment
            {
                StartPoint = midPoint,
                EndPoint = endPoint,
                Center = (_operation.CenterX, _operation.CenterY),
                Radius = actualRadius,
                IsClockwise = _operation.Direction == MillingDirection.Clockwise
            };
        }

        private class CircleArcSegment : IArcSegment
        {
            public (double x, double y) StartPoint { get; set; }
            public (double x, double y) EndPoint { get; set; }
            public (double x, double y) Center { get; set; }
            public double Radius { get; set; }
            public bool IsClockwise { get; set; }
        }
    }
}

