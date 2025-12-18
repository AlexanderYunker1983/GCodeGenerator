using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для круглого кармана.
    /// </summary>
    public class CirclePocketGeometry : IPocketGeometry
    {
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
                effectiveRadius = 0.001; // Минимальный радиус для избежания ошибок

            return new CircleContour(_operation.CenterX, _operation.CenterY, effectiveRadius);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            double dx = x - _operation.CenterX;
            double dy = y - _operation.CenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            double effectiveRadius = _operation.Radius - toolRadius - taperOffset;
            return dist <= effectiveRadius + 1e-6; // Небольшой допуск для численных ошибок
        }

        public IPocketGeometry ApplyRoughingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.TotalDepth -= allowance;
            newOp.Radius -= allowance;
            return new CirclePocketGeometry(newOp);
        }

        public IPocketGeometry ApplyBottomFinishingAllowance(double allowance)
        {
            var newOp = CloneOperation();
            newOp.Radius -= allowance;
            return new CirclePocketGeometry(newOp);
        }

        public bool IsTooSmall()
        {
            return _operation.Radius <= 0;
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
            return effectiveDiameter < minSizeThreshold - 1e-6;
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

        private PocketCircleOperation CloneOperation()
        {
            return new PocketCircleOperation
            {
                Name = _operation.Name,
                IsEnabled = _operation.IsEnabled,
                PocketStrategy = _operation.PocketStrategy,
                Direction = _operation.Direction,
                CenterX = _operation.CenterX,
                CenterY = _operation.CenterY,
                Radius = _operation.Radius,
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

            public double GetPerimeter()
            {
                return 2 * Math.PI * _radius;
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

