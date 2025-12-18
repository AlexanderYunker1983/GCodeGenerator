using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для эллиптического кармана.
    /// </summary>
    public class EllipsePocketGeometry : IPocketGeometry
    {
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
            
            if (effectiveRadiusX <= 0) effectiveRadiusX = 0.001;
            if (effectiveRadiusY <= 0) effectiveRadiusY = 0.001;

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
            
            return dist <= 1.0 + 1e-6; // Небольшой допуск для численных ошибок
        }

        public IPocketGeometry ApplyRoughingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.TotalDepth -= allowance;
            newOp.RadiusX -= allowance;
            newOp.RadiusY -= allowance;
            return new EllipsePocketGeometry(newOp);
        }

        public IPocketGeometry ApplyBottomFinishingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.RadiusX -= allowance;
            newOp.RadiusY -= allowance;
            return new EllipsePocketGeometry(newOp);
        }

        public bool IsTooSmall()
        {
            return _operation.RadiusX <= 0 || _operation.RadiusY <= 0;
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

        private PocketEllipseOperation CloneOperation()
        {
            return new PocketEllipseOperation
            {
                Name = _operation.Name,
                IsEnabled = _operation.IsEnabled,
                PocketStrategy = _operation.PocketStrategy,
                Direction = _operation.Direction,
                CenterX = _operation.CenterX,
                CenterY = _operation.CenterY,
                RadiusX = _operation.RadiusX,
                RadiusY = _operation.RadiusY,
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

            public double GetPerimeter()
            {
                // Приближенная формула Рамануджана для периметра эллипса
                double h = Math.Pow(_radiusX - _radiusY, 2) / Math.Pow(_radiusX + _radiusY, 2);
                return Math.PI * (_radiusX + _radiusY) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
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

