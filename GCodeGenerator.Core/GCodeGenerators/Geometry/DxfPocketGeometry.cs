using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
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

        // Кеш последнего построенного эквидистантного контура и центра исходного
        // контура. В пределах одного слоя смещение одинаково для всех вызовов
        // (GetContour, IsContourTooSmall, HasWindingDirectionChanged,
        // HasVectorDirectionChanged и IsPointInside на каждую точку траектории
        // стратегии), а построение эквидистанты линейно по числу вершин —
        // без кеша спираль по контуру из N вершин стоила O(точек спирали x N).
        // Экземпляр геометрии живёт в пределах одного слоя одного контура,
        // а точки контура за это время не меняются, поэтому кешируется только
        // последнее значение смещения; при смене смещения контур строится заново.
        private bool _hasCachedOffsetContour;
        private double _cachedOffset;
        private DxfPolyline _cachedOffsetContour;
        private bool _hasCachedCenter;
        private (double x, double y) _cachedCenter;
        private bool _hasCachedHullWidth;
        private double _cachedHullWidth;

        public DxfPocketGeometry(PocketDxfOperation operation, DxfPolyline primaryContour = null)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));

            // Используем первый контур как основной, если не указан явно
            _primaryContour = primaryContour ??
                (operation.ClosedContours != null && operation.ClosedContours.Count > 0
                    ? operation.ClosedContours[0]
                    : null);
        }

        /// <summary>
        /// Строит эквидистанту исходного контура, повторно используя результат
        /// предыдущего вызова с тем же смещением (см. описание полей кеша).
        /// </summary>
        private DxfPolyline GetOffsetContour(double offset)
        {
            if (_hasCachedOffsetContour && _cachedOffset.Equals(offset))
                return _cachedOffsetContour;

            _cachedOffsetContour = OffsetContour(_primaryContour, offset);
            _cachedOffset = offset;
            _hasCachedOffsetContour = true;
            return _cachedOffsetContour;
        }

        public (double x, double y) GetCenter()
        {
            if (_hasCachedCenter)
                return _cachedCenter;

            _cachedCenter = CalculateCenter();
            _hasCachedCenter = true;
            return _cachedCenter;
        }

        private (double x, double y) CalculateCenter()
            => Geometry2D.Centroid(_primaryContour?.Points, GeometryTolerances.Vertex);

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return new EmptyContour();

            // Для DXF кармана смещение контура выполняется через увеличение радиуса инструмента
            // В генераторе используется: effectiveToolRadius = toolRadius + offset
            // И затем контур смещается внутрь на effectiveToolRadius
            double effectiveToolRadius = toolRadius + taperOffset;
            
            // Смещаем контур внутрь на effectiveToolRadius
            var offsetContour = GetOffsetContour(-effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return new EmptyContour();

            return new DxfContour(offsetContour);
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return false;

            double effectiveToolRadius = toolRadius + taperOffset;
            var offsetContour = GetOffsetContour(-effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return false;

            return IsPointInsideContour(x, y, offsetContour);
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return true;

            double effectiveToolRadius = toolRadius + taperOffset;

            // Любая область, в которую физически помещается круглая фреза,
            // должна иметь ширину не меньше диаметра фрезы в каждом направлении.
            // Проверяем минимальную ширину выпуклой оболочки ДО построения оффсета:
            // legacy-алгоритм OffsetContour после перехода через вырождение может
            // вернуть bowtie или маленький инвертированный многоугольник с ненулевой
            // shoelace-площадью, и последующие эвристики ошибочно считают его валидным.
            if (effectiveToolRadius > 0
                && GetMinimumConvexHullWidth() + GeometryTolerances.Vertex < 2.0 * effectiveToolRadius)
            {
                return true;
            }
            
            // Смещаем контур внутрь на effectiveToolRadius
            var offsetContour = GetOffsetContour(-effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return true;

            // Вычисляем площадь смещенного контура
            double offsetArea = GetContourArea(offsetContour);
            
            // Проверяем, что смещенный контур не вырожден (имеет достаточную площадь)
            double minArea = GeometryTolerances.Vertex; // Минимальная площадь для невырожденного контура
            if (Math.Abs(offsetArea) < minArea)
                return true;

            // Проверяем инверсию контура: если вектор хотя бы одной из вершин до центра масс
            // поменял направление на 180±30 градусов - контур инвертировался
            var originalCenter = GetCenter();
            var offsetCenter = GetContourCenter(offsetContour);
            
            var originalPoints = _primaryContour.Points;
            var offsetPoints = offsetContour.Points;
            
            // Проверяем каждую вершину исходного контура
            // Находим ближайшую точку в смещенном контуре для каждой вершины исходного контура
            double toleranceDegrees = 30.0; // Допуск ±30 градусов
            double minAngleChange = 180.0 - toleranceDegrees; // 150 градусов
            double maxAngleChange = 180.0 + toleranceDegrees; // 210 градусов
            
            for (int i = 0; i < originalPoints.Count; i++)
            {
                var origPoint = originalPoints[i];
                
                // Вектор от центра до вершины исходного контура
                double origDx = origPoint.X - originalCenter.x;
                double origDy = origPoint.Y - originalCenter.y;
                
                // Пропускаем точки слишком близко к центру
                double origDist = Math.Sqrt(origDx * origDx + origDy * origDy);
                if (origDist < GeometryTolerances.Vertex)
                    continue;
                
                // Находим ближайшую точку в смещенном контуре
                int closestOffsetIdx = 0;
                double minDist = double.MaxValue;
                for (int j = 0; j < offsetPoints.Count; j++)
                {
                    double dx = offsetPoints[j].X - origPoint.X;
                    double dy = offsetPoints[j].Y - origPoint.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestOffsetIdx = j;
                    }
                }
                
                var offsetPoint = offsetPoints[closestOffsetIdx];
                
                // Вектор от центра до соответствующей вершины смещенного контура
                double offsetDx = offsetPoint.X - offsetCenter.x;
                double offsetDy = offsetPoint.Y - offsetCenter.y;
                
                // Пропускаем точки слишком близко к центру
                double offsetDist = Math.Sqrt(offsetDx * offsetDx + offsetDy * offsetDy);
                if (offsetDist < GeometryTolerances.Vertex)
                    continue;
                
                // Вычисляем углы векторов (в радианах)
                double origAngle = Math.Atan2(origDy, origDx);
                double offsetAngle = Math.Atan2(offsetDy, offsetDx);
                
                // Вычисляем изменение угла (учитываем направление)
                double angleChange = offsetAngle - origAngle;
                
                // Нормализуем к диапазону [-π, π]
                while (angleChange > Math.PI)
                    angleChange -= 2 * Math.PI;
                while (angleChange < -Math.PI)
                    angleChange += 2 * Math.PI;
                
                // Берем абсолютное значение
                double angleChangeAbs = Math.Abs(angleChange);
                
                // Переводим в градусы
                double angleChangeDegrees = angleChangeAbs * 180.0 / Math.PI;
                
                // Если угол изменился на 180±30 градусов, контур инвертировался
                if (angleChangeDegrees >= minAngleChange && angleChangeDegrees <= maxAngleChange)
                {
                    return true;
                }
            }

            // Контур валиден - не вырожден и не инвертирован
            return false;
        }

        /// <summary>
        /// Минимальная ширина выпуклой оболочки исходного контура. Не зависит от
        /// смещения, поэтому вычисляется один раз на экземпляр геометрии.
        /// </summary>
        private double GetMinimumConvexHullWidth()
        {
            if (_hasCachedHullWidth)
                return _cachedHullWidth;

            _cachedHullWidth = CalculateMinimumConvexHullWidth(_primaryContour);
            _hasCachedHullWidth = true;
            return _cachedHullWidth;
        }

        /// <summary>
        /// Возвращает минимальную ширину выпуклой оболочки контура.
        /// Это безопасная необходимая проверка вместимости круглого инструмента:
        /// если даже выпуклая оболочка уже диаметра фрезы, исходный контур тем более
        /// не может содержать требуемую окружность.
        /// </summary>
        private static double CalculateMinimumConvexHullWidth(DxfPolyline contour)
        {
            var points = contour.Points
                .Where(point => point != null && double.IsFinite(point.X) && double.IsFinite(point.Y))
                .Select(point => (x: point.X, y: point.Y))
                .Distinct()
                .OrderBy(point => point.x)
                .ThenBy(point => point.y)
                .ToList();

            if (points.Count < 3)
                return 0;

            var lower = new List<(double x, double y)>();
            foreach (var point in points)
            {
                while (lower.Count >= 2
                    && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= GeometryTolerances.Degenerate)
                {
                    lower.RemoveAt(lower.Count - 1);
                }
                lower.Add(point);
            }

            var upper = new List<(double x, double y)>();
            for (int index = points.Count - 1; index >= 0; index--)
            {
                var point = points[index];
                while (upper.Count >= 2
                    && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= GeometryTolerances.Degenerate)
                {
                    upper.RemoveAt(upper.Count - 1);
                }
                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            var hull = lower.Concat(upper).ToList();
            if (hull.Count < 3)
                return 0;

            double minimumWidth = double.PositiveInfinity;
            int antipodalIndex = 1;
            for (int edgeIndex = 0; edgeIndex < hull.Count; edgeIndex++)
            {
                var edgeStart = hull[edgeIndex];
                var edgeEnd = hull[(edgeIndex + 1) % hull.Count];
                double edgeDx = edgeEnd.x - edgeStart.x;
                double edgeDy = edgeEnd.y - edgeStart.y;
                double edgeLength = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
                if (edgeLength <= GeometryTolerances.Degenerate)
                    continue;

                while (true)
                {
                    int nextIndex = (antipodalIndex + 1) % hull.Count;
                    double currentArea = Math.Abs(Cross(edgeStart, edgeEnd, hull[antipodalIndex]));
                    double nextArea = Math.Abs(Cross(edgeStart, edgeEnd, hull[nextIndex]));
                    if (nextArea <= currentArea + GeometryTolerances.Degenerate)
                        break;
                    antipodalIndex = nextIndex;
                }

                double width = Math.Abs(Cross(edgeStart, edgeEnd, hull[antipodalIndex])) / edgeLength;
                minimumWidth = Math.Min(minimumWidth, width);
            }

            return double.IsFinite(minimumWidth) ? minimumWidth : 0;
        }

        private static double Cross(
            (double x, double y) origin,
            (double x, double y) first,
            (double x, double y) second)
        {
            return (first.x - origin.x) * (second.y - origin.y)
                - (first.y - origin.y) * (second.x - origin.x);
        }

        /// <summary>
        /// Проверяет, изменилось ли направление обхода контура (по знаку площади).
        /// </summary>
        /// <param name="toolRadius">Радиус инструмента</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок</param>
        /// <returns>true, если направление обхода изменилось</returns>
        public bool HasWindingDirectionChanged(double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return false;

            // Вычисляем знак площади исходного контура
            double originalSignedArea = GetSignedArea(_primaryContour);
            
            // Смещаем контур внутрь на effectiveToolRadius
            double effectiveToolRadius = toolRadius + taperOffset;
            var offsetContour = GetOffsetContour(-effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return false;

            // Вычисляем знак площади смещенного контура
            double offsetSignedArea = GetSignedArea(offsetContour);
            
            // Если знаки разные - направление обхода изменилось
            return Math.Sign(originalSignedArea) != Math.Sign(offsetSignedArea);
        }

        /// <summary>
        /// Проверяет, изменился ли хотя бы один вектор от вершины до центра на 180±30 градусов.
        /// </summary>
        /// <param name="toolRadius">Радиус инструмента</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок</param>
        /// <returns>true, если хотя бы один вектор изменил направление</returns>
        public bool HasVectorDirectionChanged(double toolRadius, double taperOffset)
        {
            if (_primaryContour == null || _primaryContour.Points == null || _primaryContour.Points.Count < 3)
                return false;

            double effectiveToolRadius = toolRadius + taperOffset;
            
            // Смещаем контур внутрь на effectiveToolRadius
            var offsetContour = GetOffsetContour(-effectiveToolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return false;

            var originalCenter = GetCenter();
            var offsetCenter = GetContourCenter(offsetContour);
            
            var originalPoints = _primaryContour.Points;
            var offsetPoints = offsetContour.Points;
            
            // Проверяем каждую вершину исходного контура
            double toleranceDegrees = 30.0; // Допуск ±30 градусов
            double minAngleChange = 180.0 - toleranceDegrees; // 150 градусов
            double maxAngleChange = 180.0 + toleranceDegrees; // 210 градусов
            
            for (int i = 0; i < originalPoints.Count; i++)
            {
                var origPoint = originalPoints[i];
                
                // Вектор от центра до вершины исходного контура
                double origDx = origPoint.X - originalCenter.x;
                double origDy = origPoint.Y - originalCenter.y;
                
                // Пропускаем точки слишком близко к центру
                double origDist = Math.Sqrt(origDx * origDx + origDy * origDy);
                if (origDist < GeometryTolerances.Vertex)
                    continue;
                
                // Находим ближайшую точку в смещенном контуре
                int closestOffsetIdx = 0;
                double minDist = double.MaxValue;
                for (int j = 0; j < offsetPoints.Count; j++)
                {
                    double dx = offsetPoints[j].X - origPoint.X;
                    double dy = offsetPoints[j].Y - origPoint.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestOffsetIdx = j;
                    }
                }
                
                var offsetPoint = offsetPoints[closestOffsetIdx];
                
                // Вектор от центра до соответствующей вершины смещенного контура
                double offsetDx = offsetPoint.X - offsetCenter.x;
                double offsetDy = offsetPoint.Y - offsetCenter.y;
                
                // Пропускаем точки слишком близко к центру
                double offsetDist = Math.Sqrt(offsetDx * offsetDx + offsetDy * offsetDy);
                if (offsetDist < GeometryTolerances.Vertex)
                    continue;
                
                // Вычисляем углы векторов (в радианах)
                double origAngle = Math.Atan2(origDy, origDx);
                double offsetAngle = Math.Atan2(offsetDy, offsetDx);
                
                // Вычисляем изменение угла (учитываем направление)
                double angleChange = offsetAngle - origAngle;
                
                // Нормализуем к диапазону [-π, π]
                while (angleChange > Math.PI)
                    angleChange -= 2 * Math.PI;
                while (angleChange < -Math.PI)
                    angleChange += 2 * Math.PI;
                
                // Берем абсолютное значение
                double angleChangeAbs = Math.Abs(angleChange);
                
                // Переводим в градусы
                double angleChangeDegrees = angleChangeAbs * 180.0 / Math.PI;
                
                // Если угол изменился на 180±30 градусов, вектор изменил направление
                if (angleChangeDegrees >= minAngleChange && angleChangeDegrees <= maxAngleChange)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Вычисляет знаковую площадь контура (положительная для против часовой стрелки, отрицательная для по часовой).
        /// </summary>
        private double GetSignedArea(DxfPolyline contour)
            => Geometry2D.SignedArea(contour?.Points);

        /// <summary>
        /// Вычисляет центр масс (центроид) контура.
        /// </summary>
        private (double x, double y) GetContourCenter(DxfPolyline contour)
            => contour?.Points == null || contour.Points.Count < 3
                ? (0, 0)
                : Geometry2D.Centroid(contour.Points, GeometryTolerances.Vertex);

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
            const double tolerance = GeometryTolerances.Vertex;

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
                    End = offsetP2
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
        }

        /// <summary>
        /// Находит точку пересечения двух отрезков.
        /// </summary>
        private DxfPoint FindLineSegmentIntersection(
            double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4,
            double tolerance)
            => Geometry2D.SegmentIntersectionPoint(
                x1, y1, x2, y2, x3, y3, x4, y4, tolerance, tolerance);

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
            => Geometry2D.PointsMatch(p1, p2, GeometryTolerances.PointCoincidence);

        private bool IsPointInsideContour(double x, double y, DxfPolyline contour)
            => Geometry2D.IsPointInsidePolygon(x, y, contour?.Points);

        private double GetContourArea(DxfPolyline contour)
            => Geometry2D.Area(contour?.Points);

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
        }
    }
}

