using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для DXF кармана.
    /// Работает с замкнутыми контурами из DXF файла.
    /// </summary>
    public class DxfPocketGeometry : IPocketGeometry
    {
        private readonly PocketDxfOperation _operation;
        private readonly DxfPolyline _primaryContour;

        public DxfPocketGeometry(PocketDxfOperation operation, DxfPolyline primaryContour = null)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
            
            // Используем первый контур как основной, если не указан явно
            _primaryContour = primaryContour ?? 
                (operation.ClosedContours != null && operation.ClosedContours.Count > 0 
                    ? operation.ClosedContours[0] 
                    : null);
        }

        public (double x, double y) GetCenter()
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count == 0)
                return (0, 0);

            double sumX = 0, sumY = 0;
            foreach (var p in _primaryContour.Points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            return (sumX / _primaryContour.Points.Count, sumY / _primaryContour.Points.Count);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return new EmptyContour();

            // Для DXF кармана смещение контура выполняется через увеличение радиуса инструмента
            // В генераторе используется: effectiveToolRadius = toolRadius + offset
            // И затем контур смещается внутрь на effectiveToolRadius
            double effectiveToolRadius = toolRadius + taperOffset;
            
            // Смещаем контур внутрь на effectiveToolRadius
            var offsetContour = OffsetContour(_primaryContour, -effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return new EmptyContour();

            return new DxfContour(offsetContour);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return false;

            double effectiveToolRadius = toolRadius + taperOffset;
            var offsetContour = OffsetContour(_primaryContour, -effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return false;

            return IsPointInsideContour(x, y, offsetContour);
        }

        public IPocketGeometry ApplyRoughingAllowance(double allowance)
        {
            // Для DXF припуск применяется через увеличение диаметра инструмента
            // В генераторе: roughOp.ToolDiameter += 2 * depthAllowance
            // Это эквивалентно уменьшению контура на allowance
            var newOp = CloneOperation();
            newOp.TotalDepth -= allowance;
            // Припуск по контуру будет применен через увеличение toolRadius в GetContour
            return new DxfPocketGeometry(newOp, _primaryContour);
        }

        public IPocketGeometry ApplyBottomFinishingAllowance(double allowance)
        {
            // Для чистовой обработки дна также применяем через смещение контура
            var newOp = CloneOperation();
            return new DxfPocketGeometry(newOp, _primaryContour);
        }

        public bool IsTooSmall()
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return true;

            // Проверяем площадь контура
            double area = GetContourArea(_primaryContour);
            return area <= 0.001 * 0.001; // Минимальная площадь
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

        private PocketDxfOperation CloneOperation()
        {
            return new PocketDxfOperation
            {
                Name = _operation.Name,
                IsEnabled = _operation.IsEnabled,
                ClosedContours = _operation.ClosedContours,
                DxfFilePath = _operation.DxfFilePath,
                Direction = _operation.Direction,
                PocketStrategy = _operation.PocketStrategy,
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
        /// Смещает контур на заданное расстояние (положительное - наружу, отрицательное - внутрь).
        /// Упрощенная реализация для DXF контуров.
        /// </summary>
        private DxfPolyline OffsetContour(DxfPolyline contour, double offset)
        {
            if (contour?.Points == null || contour.Points.Count < 3)
                return null;

            // Определяем направление обхода контура по знаку площади
            double signedArea = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                signedArea += p1.X * p2.Y - p2.X * p1.Y;
            }
            bool isClockwise = signedArea < 0;
            double absOffset = Math.Abs(offset);

            var offsetPoints = new List<DxfPoint>();
            
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                var p0 = contour.Points[(i - 1 + contour.Points.Count) % contour.Points.Count];

                // Вычисляем нормали к сегментам
                double dx1 = p1.X - p0.X;
                double dy1 = p1.Y - p0.Y;
                double len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                if (len1 < 1e-9) len1 = 1e-9;
                double nx1 = -dy1 / len1;
                double ny1 = dx1 / len1;

                double dx2 = p2.X - p1.X;
                double dy2 = p2.Y - p1.Y;
                double len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                if (len2 < 1e-9) len2 = 1e-9;
                double nx2 = -dy2 / len2;
                double ny2 = dx2 / len2;

                // Направляем нормали внутрь контура
                if (isClockwise)
                {
                    nx1 = -nx1;
                    ny1 = -ny1;
                    nx2 = -nx2;
                    ny2 = -ny2;
                }
                if (offset > 0) // Если offset положительный (наружу), инвертируем
                {
                    nx1 = -nx1;
                    ny1 = -ny1;
                    nx2 = -nx2;
                    ny2 = -ny2;
                }

                // Средняя нормаль в вершине
                double nx = (nx1 + nx2) / 2.0;
                double ny = (ny1 + ny2) / 2.0;
                double normLen = Math.Sqrt(nx * nx + ny * ny);
                if (normLen > 1e-9)
                {
                    nx /= normLen;
                    ny /= normLen;
                }

                // Смещаем точку
                offsetPoints.Add(new DxfPoint
                {
                    X = p1.X + nx * absOffset,
                    Y = p1.Y + ny * absOffset
                });
            }

            if (offsetPoints.Count >= 3)
            {
                // Замыкаем контур
                if (!PointsMatch(offsetPoints[0], offsetPoints[offsetPoints.Count - 1]))
                {
                    offsetPoints.Add(new DxfPoint 
                    { 
                        X = offsetPoints[0].X, 
                        Y = offsetPoints[0].Y 
                    });
                }
                return new DxfPolyline { Points = offsetPoints };
            }

            return null;
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= 0.001;
        }

        private bool IsPointInsideContour(double x, double y, DxfPolyline contour)
        {
            // Ray casting algorithm для проверки, находится ли точка внутри полигона
            if (contour?.Points == null || contour.Points.Count < 3)
                return false;

            bool inside = false;
            for (int i = 0, j = contour.Points.Count - 1; i < contour.Points.Count; j = i++)
            {
                var pi = contour.Points[i];
                var pj = contour.Points[j];
                
                if (((pi.Y > y) != (pj.Y > y)) &&
                    (x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private double GetContourArea(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 3)
                return 0;

            double area = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(area / 2.0);
        }

        /// <summary>
        /// Реализация контура для DXF полилинии.
        /// </summary>
        private class DxfContour : IContour
        {
            private readonly DxfPolyline _polyline;

            public DxfContour(DxfPolyline polyline)
            {
                _polyline = polyline ?? throw new ArgumentNullException(nameof(polyline));
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                if (_polyline?.Points == null)
                    yield break;

                foreach (var point in _polyline.Points)
                {
                    yield return (point.X, point.Y);
                }
            }

            public double GetArea()
            {
                if (_polyline?.Points == null || _polyline.Points.Count < 3)
                    return 0;

                double area = 0;
                for (int i = 0; i < _polyline.Points.Count; i++)
                {
                    var p1 = _polyline.Points[i];
                    var p2 = _polyline.Points[(i + 1) % _polyline.Points.Count];
                    area += p1.X * p2.Y - p2.X * p1.Y;
                }
                return Math.Abs(area / 2.0);
            }

            public double GetPerimeter()
            {
                if (_polyline?.Points == null || _polyline.Points.Count < 2)
                    return 0;

                double perimeter = 0;
                for (int i = 0; i < _polyline.Points.Count; i++)
                {
                    var p1 = _polyline.Points[i];
                    var p2 = _polyline.Points[(i + 1) % _polyline.Points.Count];
                    double dx = p2.X - p1.X;
                    double dy = p2.Y - p1.Y;
                    perimeter += Math.Sqrt(dx * dx + dy * dy);
                }
                return perimeter;
            }
        }

        /// <summary>
        /// Пустой контур для случаев, когда контур недоступен.
        /// </summary>
        private class EmptyContour : IContour
        {
            public IEnumerable<(double x, double y)> GetPoints()
            {
                yield break;
            }

            public double GetArea()
            {
                return 0;
            }

            public double GetPerimeter()
            {
                return 0;
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

