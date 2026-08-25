using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для круглого кармана.
    /// </summary>
    public class CirclePocketGeometry : IPocketGeometry
    {
        /// <summary>Одна фигура — одна область: перемычкам взяться неоткуда.</summary>
        public bool SplitsIntoAreas => false;

        /// <inheritdoc />
        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
            => System.Array.Empty<IPocketGeometry>();

        private readonly PocketCircleOperation _operation;

        public CirclePocketGeometry(PocketCircleOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public (double x, double y) GetCenter()
        {
            return (_operation.CenterX, _operation.CenterY);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            double effectiveRadius = _operation.Radius - toolRadius - taperOffset;
            if (effectiveRadius <= 0)
                effectiveRadius = GeometryTolerances.MinimumContourExtent;

            return new CircleContour(_operation.CenterX, _operation.CenterY, effectiveRadius);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            double dx = x - _operation.CenterX;
            double dy = y - _operation.CenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double effectiveRadius = _operation.Radius - toolRadius - taperOffset;
            return dist <= effectiveRadius + GeometryTolerances.Containment;
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
        {
            // Вычисляем эффективный радиус (уже с учетом фрезы и уклона)
            double effectiveRadius = _operation.Radius - toolRadius - taperOffset;
            double toolDiameter = toolRadius * 2.0;
            double effectiveDiameter = effectiveRadius * 2.0;
            
            // Минимальный порог размера контура: 5% от диаметра фрезы
            double minSizeThreshold = toolDiameter * 0.05;
            
            // Контур слишком маленький, если эффективный диаметр меньше минимального порога
            return effectiveDiameter < minSizeThreshold - GeometryTolerances.Vertex;
        }

        /// <summary>
        /// Реализация контура для круга.
        /// </summary>
        private class CircleContour : IContour
        {
            private readonly double _centerX;
            private readonly double _centerY;
            private readonly double _radius;

            public CircleContour(double centerX, double centerY, double radius)
            {
                _centerX = centerX;
                _centerY = centerY;
                _radius = Math.Max(0, radius);
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                // Генерируем точки окружности с достаточным количеством сегментов
                int segments = Math.Max(32, (int)Math.Ceiling(2 * Math.PI * _radius / 0.5));
                if (segments < 4) segments = 4;

                for (int i = 0; i <= segments; i++)
                {
                    double angle = 2 * Math.PI * i / segments;
                    yield return (
                        _centerX + _radius * Math.Cos(angle),
                        _centerY + _radius * Math.Sin(angle)
                    );
                }
            }

            public double GetArea()
            {
                return Math.PI * _radius * _radius;
            }
        }
    }
}

