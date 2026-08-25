#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для прямоугольного кармана.
    /// </summary>
    public class RectanglePocketGeometry : IPocketGeometry
    {
        /// <summary>Одна фигура — одна область: перемычкам взяться неоткуда.</summary>
        public bool SplitsIntoAreas => false;

        /// <inheritdoc />
        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
            => System.Array.Empty<IPocketGeometry>();

        private readonly PocketRectangleOperation _operation;

        public RectanglePocketGeometry(PocketRectangleOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public (double x, double y) GetCenter()
        {
            GetCenter(_operation.ReferencePointType, _operation.ReferencePointX, _operation.ReferencePointY,
                _operation.Width, _operation.Height, out double cx, out double cy);
            return (cx, cy);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            var (cx, cy) = GetCenter();
            double effectiveToolRadius = toolRadius + taperOffset;
            
            double baseHalfW = _operation.Width / 2.0;
            double baseHalfH = _operation.Height / 2.0;
            
            double halfW = baseHalfW - effectiveToolRadius;
            double halfH = baseHalfH - effectiveToolRadius;
            
            if (halfW <= 0) halfW = GeometryTolerances.MinimumContourExtent;
            if (halfH <= 0) halfH = GeometryTolerances.MinimumContourExtent;

            return new RectangleContour(cx, cy, halfW, halfH, _operation.RotationAngle);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            var (cx, cy) = GetCenter();
            
            // Переводим точку в локальные координаты (относительно центра)
            double dx = x - cx;
            double dy = y - cy;
            
            // Учитываем поворот
            double angleRad = _operation.RotationAngle * Math.PI / 180.0;
            double cos = Math.Cos(-angleRad); // Обратный поворот
            double sin = Math.Sin(-angleRad);
            
            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;
            
            double effectiveToolRadius = toolRadius + taperOffset;
            double baseHalfW = _operation.Width / 2.0;
            double baseHalfH = _operation.Height / 2.0;
            
            double halfW = baseHalfW - effectiveToolRadius;
            double halfH = baseHalfH - effectiveToolRadius;
            
            return Math.Abs(localX) <= halfW + GeometryTolerances.Containment
                && Math.Abs(localY) <= halfH + GeometryTolerances.Containment;
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
        {
            double effectiveToolRadius = toolRadius + taperOffset;
            double toolDiameter = toolRadius * 2.0;
            
            // Минимальный порог размера контура: 5% от диаметра фрезы
            double minSizeThreshold = toolDiameter * 0.05;
            
            // Вычисляем эффективные размеры (уже с учетом фрезы и уклона)
            double effectiveWidth = _operation.Width - 2 * effectiveToolRadius;
            double effectiveHeight = _operation.Height - 2 * effectiveToolRadius;
            
            // Контур слишком маленький, если ширина или высота меньше минимального порога
            return effectiveWidth < minSizeThreshold - GeometryTolerances.Vertex
                || effectiveHeight < minSizeThreshold - GeometryTolerances.Vertex;
        }

        private void GetCenter(ReferencePointType type,
                               double refX, double refY,
                               double width, double height,
                               out double cx, out double cy)
        {
            switch (type)
            {
                case ReferencePointType.Center:
                    cx = refX;
                    cy = refY;
                    break;
                case ReferencePointType.TopLeft:
                    cx = refX + width / 2.0;
                    cy = refY - height / 2.0;
                    break;
                case ReferencePointType.TopRight:
                    cx = refX - width / 2.0;
                    cy = refY - height / 2.0;
                    break;
                case ReferencePointType.BottomLeft:
                    cx = refX + width / 2.0;
                    cy = refY + height / 2.0;
                    break;
                case ReferencePointType.BottomRight:
                    cx = refX - width / 2.0;
                    cy = refY + height / 2.0;
                    break;
                default:
                    cx = refX;
                    cy = refY;
                    break;
            }
        }

        /// <summary>
        /// Реализация контура для прямоугольника.
        /// </summary>
        private class RectangleContour : IContour
        {
            private readonly double _centerX;
            private readonly double _centerY;
            private readonly double _halfWidth;
            private readonly double _halfHeight;
            private readonly double _rotationAngle;

            public RectangleContour(double centerX, double centerY, double halfWidth, double halfHeight, double rotationAngle)
            {
                _centerX = centerX;
                _centerY = centerY;
                _halfWidth = Math.Max(0, halfWidth);
                _halfHeight = Math.Max(0, halfHeight);
                _rotationAngle = rotationAngle;
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                var corners = new[]
                {
                    (-_halfWidth, -_halfHeight),
                    (_halfWidth, -_halfHeight),
                    (_halfWidth, _halfHeight),
                    (-_halfWidth, _halfHeight)
                };

                double angleRad = _rotationAngle * Math.PI / 180.0;
                double cos = Math.Cos(angleRad);
                double sin = Math.Sin(angleRad);

                foreach (var (x, y) in corners)
                {
                    double rx = x * cos - y * sin;
                    double ry = x * sin + y * cos;
                    yield return (_centerX + rx, _centerY + ry);
                }
                
                // Замыкаем контур
                var first = corners[0];
                double firstRx = first.Item1 * cos - first.Item2 * sin;
                double firstRy = first.Item1 * sin + first.Item2 * cos;
                yield return (_centerX + firstRx, _centerY + firstRy);
            }

            public double GetArea()
            {
                return 4 * _halfWidth * _halfHeight;
            }
        }
    }
}

