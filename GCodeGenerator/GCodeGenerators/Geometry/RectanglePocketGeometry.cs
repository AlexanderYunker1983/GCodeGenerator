using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для прямоугольного кармана.
    /// </summary>
    public class RectanglePocketGeometry : IPocketGeometry
    {
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
            
            if (halfW <= 0) halfW = 0.001;
            if (halfH <= 0) halfH = 0.001;

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
            
            return Math.Abs(localX) <= halfW + 1e-6 && Math.Abs(localY) <= halfH + 1e-6;
        }

        public IPocketGeometry ApplyRoughingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.TotalDepth -= allowance;
            newOp.Width -= 2 * allowance;
            newOp.Height -= 2 * allowance;
            return new RectanglePocketGeometry(newOp);
        }

        public IPocketGeometry ApplyBottomFinishingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.Width -= 2 * allowance;
            newOp.Height -= 2 * allowance;
            return new RectanglePocketGeometry(newOp);
        }

        public bool IsTooSmall()
        {
            return _operation.Width <= 0 || _operation.Height <= 0;
        }

        public IPocketOperationParameters GetParameters()
        {
            return new PocketOperationParameters
            {
                TotalDepth = _operation.TotalDepth,
                ContourHeight = _operation.ContourHeight,
                IsRoughingEnabled = _operation.IsRoughingEnabled,
                IsFinishingEnabled = _operation.IsFinishingEnabled,
                FinishAllowance = _operation.FinishAllowance
            };
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

        private PocketRectangleOperation CloneOperation()
        {
            return new PocketRectangleOperation
            {
                Name = _operation.Name,
                IsEnabled = _operation.IsEnabled,
                Direction = _operation.Direction,
                PocketStrategy = _operation.PocketStrategy,
                Width = _operation.Width,
                Height = _operation.Height,
                RotationAngle = _operation.RotationAngle,
                TotalDepth = _operation.TotalDepth,
                StepDepth = _operation.StepDepth,
                ToolDiameter = _operation.ToolDiameter,
                ContourHeight = _operation.ContourHeight,
                FeedXYRapid = _operation.FeedXYRapid,
                FeedXYWork = _operation.FeedXYWork,
                FeedZRapid = _operation.FeedZRapid,
                FeedZWork = _operation.FeedZWork,
                SafeZHeight = _operation.SafeZHeight,
                RetractHeight = _operation.RetractHeight,
                ReferencePointX = _operation.ReferencePointX,
                ReferencePointY = _operation.ReferencePointY,
                ReferencePointType = _operation.ReferencePointType,
                StepPercentOfTool = _operation.StepPercentOfTool,
                Decimals = _operation.Decimals,
                LineAngleDeg = _operation.LineAngleDeg,
                WallTaperAngleDeg = _operation.WallTaperAngleDeg,
                IsRoughingEnabled = _operation.IsRoughingEnabled,
                IsFinishingEnabled = _operation.IsFinishingEnabled,
                FinishAllowance = _operation.FinishAllowance,
                FinishingMode = _operation.FinishingMode
            };
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

            public double GetPerimeter()
            {
                return 2 * (_halfWidth + _halfHeight) * 2; // 2 * (width + height)
            }
        }

        /// <summary>
        /// Реализация параметров операции для клонирования.
        /// </summary>
        private class PocketOperationParameters : IPocketOperationParameters
        {
            public double TotalDepth { get; set; }
            public double ContourHeight { get; set; }
            public bool IsRoughingEnabled { get; set; }
            public bool IsFinishingEnabled { get; set; }
            public double FinishAllowance { get; set; }
        }
    }
}

