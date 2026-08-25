using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для эллиптического кармана.
    /// </summary>
    public class EllipsePocketGeometry : IPocketGeometry
    {
        /// <summary>Одна фигура — одна область: перемычкам взяться неоткуда.</summary>
        public bool SplitsIntoAreas => false;

        /// <inheritdoc />
        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
            => System.Array.Empty<IPocketGeometry>();

        private readonly PocketEllipseOperation _operation;

        public EllipsePocketGeometry(PocketEllipseOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public (double x, double y) GetCenter()
        {
            return (_operation.CenterX, _operation.CenterY);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            double effectiveRadiusX = _operation.RadiusX - toolRadius - taperOffset;
            double effectiveRadiusY = _operation.RadiusY - toolRadius - taperOffset;
            
            if (effectiveRadiusX <= 0) effectiveRadiusX = GeometryTolerances.MinimumContourExtent;
            if (effectiveRadiusY <= 0) effectiveRadiusY = GeometryTolerances.MinimumContourExtent;

            return new EllipseContour(_operation.CenterX, _operation.CenterY, 
                effectiveRadiusX, effectiveRadiusY, _operation.RotationAngle);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            // Переводим точку в локальные координаты эллипса (с учетом поворота)
            double dx = x - _operation.CenterX;
            double dy = y - _operation.CenterY;
            
            double rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(-rotationRad); // Обратный поворот
            double sinRot = Math.Sin(-rotationRad);
            
            double xLocal = dx * cosRot - dy * sinRot;
            double yLocal = dx * sinRot + dy * cosRot;
            
            double effectiveRadiusX = _operation.RadiusX - toolRadius - taperOffset;
            double effectiveRadiusY = _operation.RadiusY - toolRadius - taperOffset;
            
            // Проверка: (x/a)^2 + (y/b)^2 <= 1
            double normalizedX = xLocal / effectiveRadiusX;
            double normalizedY = yLocal / effectiveRadiusY;
            double dist = normalizedX * normalizedX + normalizedY * normalizedY;
            
            return dist <= 1.0 + GeometryTolerances.Containment;
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
        {
            double effectiveToolRadius = toolRadius + taperOffset;
            double toolDiameter = toolRadius * 2.0;
            
            // Минимальный порог размера контура: 5% от диаметра фрезы
            double minSizeThreshold = toolDiameter * 0.05;
            
            // Вычисляем эффективные диаметры (уже с учетом фрезы и уклона)
            double effectiveDiameterX = (_operation.RadiusX - effectiveToolRadius) * 2.0;
            double effectiveDiameterY = (_operation.RadiusY - effectiveToolRadius) * 2.0;
            
            // Контур слишком маленький, если любой из диаметров меньше минимального порога
            return effectiveDiameterX < minSizeThreshold - GeometryTolerances.Vertex
                || effectiveDiameterY < minSizeThreshold - GeometryTolerances.Vertex;
        }

        /// <summary>
        /// Реализация контура для эллипса.
        /// </summary>
        private class EllipseContour : IContour
        {
            private readonly double _centerX;
            private readonly double _centerY;
            private readonly double _radiusX;
            private readonly double _radiusY;
            private readonly double _rotationAngle;

            public EllipseContour(double centerX, double centerY, double radiusX, double radiusY, double rotationAngle)
            {
                _centerX = centerX;
                _centerY = centerY;
                _radiusX = Math.Max(0, radiusX);
                _radiusY = Math.Max(0, radiusY);
                _rotationAngle = rotationAngle;
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                // Используем приближенную формулу периметра для определения количества сегментов
                double h = Math.Pow(_radiusX - _radiusY, 2) / Math.Pow(_radiusX + _radiusY, 2);
                double perimeter = Math.PI * (_radiusX + _radiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
                int segments = Math.Max(32, (int)Math.Ceiling(perimeter / 0.5));
                if (segments < 8) segments = 8;

                double rotationRad = _rotationAngle * Math.PI / 180.0;
                double cosRot = Math.Cos(rotationRad);
                double sinRot = Math.Sin(rotationRad);

                for (int i = 0; i <= segments; i++)
                {
                    double t = 2 * Math.PI * i / segments;
                    double xEllipse = _radiusX * Math.Cos(t);
                    double yEllipse = _radiusY * Math.Sin(t);
                    
                    // Поворот
                    double x = _centerX + xEllipse * cosRot - yEllipse * sinRot;
                    double y = _centerY + xEllipse * sinRot + yEllipse * cosRot;
                    
                    yield return (x, y);
                }
            }

            public double GetArea()
            {
                return Math.PI * _radiusX * _radiusY;
            }
        }
    }
}

