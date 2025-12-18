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

            // Вычисляем геометрический центр (центроид) многоугольника
            // Формула: Cx = (1/6A) * Σ(xi + xi+1)(xi*yi+1 - xi+1*yi)
            //          Cy = (1/6A) * Σ(yi + yi+1)(xi*yi+1 - xi+1*yi)
            // где A = (1/2) * Σ(xi*yi+1 - xi+1*yi) - площадь многоугольника

            double area = 0;
            double cx = 0;
            double cy = 0;

            int pointCount = _primaryContour.Points.Count;
            for (int i = 0; i < pointCount; i++)
            {
                var p1 = _primaryContour.Points[i];
                var p2 = _primaryContour.Points[(i + 1) % pointCount];

                double cross = p1.X * p2.Y - p2.X * p1.Y;
                area += cross;
                cx += (p1.X + p2.X) * cross;
                cy += (p1.Y + p2.Y) * cross;
            }

            area *= 0.5;
            double tolerance = 1e-6;

            if (Math.Abs(area) > tolerance)
            {
                double invArea = 1.0 / (6.0 * area);
                return (cx * invArea, cy * invArea);
            }
            else
            {
                // Если площадь слишком мала, используем среднее арифметическое как fallback
                double sumX = 0, sumY = 0;
                foreach (var p in _primaryContour.Points)
                {
                    sumX += p.X;
                    sumY += p.Y;
                }
                return (sumX / pointCount, sumY / pointCount);
            }
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
        /// Новый алгоритм: строим параллельные прямые для каждого сегмента, находим пересечения и обрезаем.
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
            double offsetSign = offset < 0 ? 1.0 : -1.0; // Для отрицательного offset (внутрь) используем положительный знак
            double tolerance = 1e-6;

            // Шаг 1: Строим параллельные прямые для каждого сегмента
            var offsetSegments = new List<OffsetSegment>();
            int pointCount = contour.Points.Count;

            for (int i = 0; i < pointCount; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % pointCount];

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);

                if (len < tolerance)
                    continue; // Пропускаем нулевые сегменты

                // Вычисляем нормаль к сегменту (перпендикуляр, направленный влево)
                double nx = -dy / len;
                double ny = dx / len;

                // Для кармана нормаль должна быть направлена внутрь
                if (isClockwise)
                {
                    nx = -nx;
                    ny = -ny;
                }

                // Смещаем сегмент внутрь
                var offsetP1 = new DxfPoint
                {
                    X = p1.X + nx * offsetSign * absOffset,
                    Y = p1.Y + ny * offsetSign * absOffset
                };
                var offsetP2 = new DxfPoint
                {
                    X = p2.X + nx * offsetSign * absOffset,
                    Y = p2.Y + ny * offsetSign * absOffset
                };

                offsetSegments.Add(new OffsetSegment
                {
                    Start = offsetP1,
                    End = offsetP2,
                    OriginalIndex = i
                });
            }

            if (offsetSegments.Count < 2)
                return null;

            // Шаг 2: Находим точки пересечения смещенных сегментов
            // Для каждого сегмента находим пересечение с предыдущим и следующим сегментом
            var segmentStartPoints = new List<DxfPoint>();
            var segmentEndPoints = new List<DxfPoint>();

            for (int i = 0; i < offsetSegments.Count; i++)
            {
                var seg = offsetSegments[i];
                var prevSeg = offsetSegments[(i - 1 + offsetSegments.Count) % offsetSegments.Count];
                var nextSeg = offsetSegments[(i + 1) % offsetSegments.Count];

                // Находим пересечение с предыдущим сегментом (начало текущего сегмента)
                var intersectionWithPrev = FindLineSegmentIntersection(
                    prevSeg.Start.X, prevSeg.Start.Y,
                    prevSeg.End.X, prevSeg.End.Y,
                    seg.Start.X, seg.Start.Y,
                    seg.End.X, seg.End.Y,
                    tolerance);

                // Находим пересечение со следующим сегментом (конец текущего сегмента)
                var intersectionWithNext = FindLineSegmentIntersection(
                    seg.Start.X, seg.Start.Y,
                    seg.End.X, seg.End.Y,
                    nextSeg.Start.X, nextSeg.Start.Y,
                    nextSeg.End.X, nextSeg.End.Y,
                    tolerance);

                // Начало сегмента - это пересечение с предыдущим, или начало сегмента, если пересечения нет
                if (intersectionWithPrev != null)
                {
                    segmentStartPoints.Add(intersectionWithPrev);
                }
                else
                {
                    segmentStartPoints.Add(seg.Start);
                }

                // Конец сегмента - это пересечение со следующим, или конец сегмента, если пересечения нет
                if (intersectionWithNext != null)
                {
                    segmentEndPoints.Add(intersectionWithNext);
                }
                else
                {
                    segmentEndPoints.Add(seg.End);
                }
            }

            // Шаг 3: Составляем новый контур из обрезанных сегментов
            var resultPoints = new List<DxfPoint>();

            for (int i = 0; i < offsetSegments.Count; i++)
            {
                var startPoint = segmentStartPoints[i];
                var endPoint = segmentEndPoints[i];

                // Добавляем начальную точку сегмента (если она отличается от последней добавленной)
                if (resultPoints.Count == 0 || !PointsMatch(resultPoints[resultPoints.Count - 1], startPoint))
                {
                    resultPoints.Add(startPoint);
                }

                // Добавляем конечную точку сегмента (если она отличается от последней добавленной)
                if (!PointsMatch(resultPoints[resultPoints.Count - 1], endPoint))
                {
                    resultPoints.Add(endPoint);
                }
            }

            // Удаляем дубликаты
            var cleanedPoints = new List<DxfPoint>();
            for (int i = 0; i < resultPoints.Count; i++)
            {
                if (cleanedPoints.Count == 0 || !PointsMatch(cleanedPoints[cleanedPoints.Count - 1], resultPoints[i]))
                {
                    cleanedPoints.Add(resultPoints[i]);
                }
            }

            // Замыкаем контур
            if (cleanedPoints.Count >= 3 && !PointsMatch(cleanedPoints[0], cleanedPoints[cleanedPoints.Count - 1]))
            {
                cleanedPoints.Add(new DxfPoint
                {
                    X = cleanedPoints[0].X,
                    Y = cleanedPoints[0].Y
                });
            }

            if (cleanedPoints.Count >= 3)
            {
                return new DxfPolyline { Points = cleanedPoints };
            }

            return null;
        }

        /// <summary>
        /// Представляет смещенный сегмент контура.
        /// </summary>
        private class OffsetSegment
        {
            public DxfPoint Start { get; set; }
            public DxfPoint End { get; set; }
            public int OriginalIndex { get; set; }
        }

        /// <summary>
        /// Представляет точку пересечения двух смещенных сегментов.
        /// </summary>
        private class IntersectionPoint
        {
            public DxfPoint Point { get; set; }
            public int SegmentIndex1 { get; set; }
            public int SegmentIndex2 { get; set; }
            public bool IsStartOfSeg1 { get; set; }
            public bool IsEndOfSeg1 { get; set; }
            public bool IsStartOfSeg2 { get; set; }
            public bool IsEndOfSeg2 { get; set; }
        }


        /// <summary>
        /// Находит точку пересечения двух отрезков.
        /// </summary>
        private DxfPoint FindLineSegmentIntersection(
            double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4,
            double tolerance)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x4 - x3;
            double dy2 = y4 - y3;

            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < tolerance)
                return null; // Параллельные линии

            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;

            // Используем небольшой допуск для границ отрезков
            if (t1 >= -tolerance && t1 <= 1.0 + tolerance && t2 >= -tolerance && t2 <= 1.0 + tolerance)
            {
                // Ограничиваем параметры диапазоном [0, 1]
                t1 = Math.Max(0, Math.Min(1, t1));
                return new DxfPoint
                {
                    X = x1 + t1 * dx1,
                    Y = y1 + t1 * dy1
                };
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

