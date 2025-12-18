using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов карманов.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// </summary>
    public class UnifiedPocketGenerator : IOperationGenerator
    {
        private readonly PocketGenerationHelper _helper;

        public UnifiedPocketGenerator()
        {
            _helper = new PocketGenerationHelper();
        }

        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            // Проверяем, что операция является карманом
            if (!(operation is IPocketOperation pocketOp))
                return;

            // Создаем геометрию кармана
            var geometry = PocketGeometryFactory.Create(operation);
            if (geometry == null)
                return;

            // Временно: генерируем только основную обработку без roughing/finishing
            GenerateInternal(pocketOp, geometry, addLine, g0, g1, settings);
        }

        /// <summary>
        /// Генерирует внутреннюю обработку кармана (без учета rough/finish).
        /// </summary>
        private void GenerateInternal(
            IPocketOperation op,
            IPocketGeometry geometry,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;
            double step = GCodeGenerationHelper.CalculateStep(op.ToolDiameter, stepPercent);

            // Генерируем цикл по слоям
            _helper.GenerateLayerLoop(
                op,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    op,
                    geometry,
                    toolRadius,
                    step,
                    currentZ,
                    nextZ,
                    addLine,
                    g0,
                    g1,
                    settings),
                addLine,
                g0,
                g1,
                settings);
        }

        /// <summary>
        /// Генерирует один слой кармана.
        /// </summary>
        private void GenerateLayer(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double step,
            double currentZ,
            double nextZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double depthFromTop = op.ContourHeight - nextZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            // Для DXF операций обрабатываем все контуры отдельно с использованием спирали
            if (op is PocketDxfOperation dxfOp)
            {
                GenerateDxfLayerWithSpiral(dxfOp, geometry, toolRadius, taperOffset, step, currentZ, nextZ, addLine, g0, g1, fmt, culture, settings);
                return;
            }

            // Получаем контур кармана
            var contour = geometry.GetContour(toolRadius, taperOffset);
            if (contour == null)
                return;

            var center = geometry.GetCenter();
            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return;

            // Перемещаемся к центру кармана
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

            // Генерируем спиральную стратегию
            GenerateSpiralStrategy(op, geometry, toolRadius, taperOffset, step, contourPoints, center, addLine, g0, g1, fmt, culture, settings);

            // Возврат в центр и подъем
            addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }

        /// <summary>
        /// Генерирует один слой для DXF кармана с несколькими контурами, используя спиральную стратегию.
        /// </summary>
        private void GenerateDxfLayerWithSpiral(
            PocketDxfOperation op,
            IPocketGeometry overallGeometry,
            double toolRadius,
            double taperOffset,
            double step,
            double currentZ,
            double nextZ,
            Action<string> addLine,
            string g0,
            string g1,
            string fmt,
            CultureInfo culture,
            GCodeSettings settings)
        {
            if (op.ClosedContours == null || op.ClosedContours.Count == 0)
                return;

            bool isFirstContour = true;
            foreach (var contour in op.ClosedContours)
            {
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

                // Создаем геометрию для этого контура
                var geometry = new DxfPocketGeometry(op, contour);
                
                // Получаем эквидистантный контур (смещенный внутрь)
                var offsetContour = geometry.GetContour(toolRadius, taperOffset);
                if (offsetContour == null)
                    continue;

                var contourPoints = offsetContour.GetPoints().ToList();
                if (contourPoints.Count == 0)
                    continue;

                // Вычисляем геометрический центр контура
                var center = geometry.GetCenter();

                // Поднимаем инструмент перед переходом к следующему контуру (кроме первого)
                if (!isFirstContour)
                {
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                // Перемещаемся к центру контура
                addLine($"{g0} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                
                // Опускаемся на рабочую высоту (только для первого контура, для остальных уже на нужной высоте)
                if (isFirstContour)
                {
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");
                }
                else
                {
                    addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }

                // Генерируем спиральную стратегию для этого контура
                GenerateSpiralStrategy(op, geometry, toolRadius, taperOffset, step, contourPoints, center, addLine, g0, g1, fmt, culture, settings);

                // Возврат в центр контура и подъем
                addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                isFirstContour = false;
            }
        }

        /// <summary>
        /// Генерирует спиральную стратегию обработки.
        /// </summary>
        private void GenerateSpiralStrategy(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            double step,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            Action<string> addLine,
            string g0,
            string g1,
            string fmt,
            CultureInfo culture,
            GCodeSettings settings)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            // Находим максимальное расстояние от центра до контура
            double maxDistance = 0.0;
            foreach (var point in contourPoints)
            {
                double dx = point.x - center.x;
                double dy = point.y - center.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > maxDistance)
                    maxDistance = distance;
            }

            if (maxDistance <= 0)
                return;

            // Параметры спирали: r = a + b*θ
            // a = 0 (начинаем с центра)
            // b = step / (2*π) (шаг спирали)
            double a = 0.0;
            double b = step / (2.0 * Math.PI);

            // Направление спирали
            double dirSign = op.Direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            // Максимальный угол для достижения внешнего радиуса
            double θMax = maxDistance / b;

            // Количество точек на оборот для плавности
            int pointsPerRevolution = 128;
            double stepAngle = 2.0 * Math.PI / pointsPerRevolution;
            double tolerance = 1e-6;

            // Начинаем с центра
            (double x, double y) currentPos = center;
            addLine($"{g1} X{currentPos.x.ToString(fmt, culture)} Y{currentPos.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            bool wasInside = true;
            (double x, double y)? exitPoint = null;
            bool finished = false;

            // Генерируем спираль
            // Используем while цикл для точного контроля угла и предотвращения пропуска витков
            double θ = stepAngle;
            double prevTheta = 0.0; // Угол предыдущей точки
            while (θ <= θMax && !finished)
            {
                double r = a + b * θ;
                double ang = θ * dirSign;
                double nextX = center.x + r * Math.Cos(ang);
                double nextY = center.y + r * Math.Sin(ang);
                (double x, double y) nextPos = (nextX, nextY);

                // Проверяем, находится ли следующая точка внутри контура
                bool isInside = geometry.IsPointInside(nextX, nextY, toolRadius, taperOffset);

                if (isInside && wasInside)
                {
                    // Обе точки внутри - просто добавляем точку
                    addLine($"{g1} X{nextX.ToString(fmt, culture)} Y{nextY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    currentPos = nextPos;
                    prevTheta = θ;
                    θ += stepAngle; // Переходим к следующей точке
                }
                else if (!isInside && wasInside)
                {
                    // Пересекли контур - вышли наружу
                    // Находим точку пересечения и точный угол в этой точке
                    var exitResult = FindExitPointWithTheta(
                        currentPos, nextPos, prevTheta, θ, contourPoints, center, a, b, dirSign, tolerance);

                    if (exitResult.HasValue)
                    {
                        exitPoint = exitResult.Value.point;
                        double exitTheta = exitResult.Value.theta;
                        
                        addLine($"{g1} X{exitPoint.Value.x.ToString(fmt, culture)} Y{exitPoint.Value.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                        currentPos = exitPoint.Value;

                        // Ищем точку повторного входа с сохранением угла
                        var reentryResult = FindReentryPointWithTheta(
                            exitPoint.Value, exitTheta, θMax, stepAngle, dirSign, center, a, b,
                            geometry, toolRadius, taperOffset, contourPoints, tolerance);

                        if (reentryResult.HasValue)
                        {
                            var reentryPoint = reentryResult.Value.point;
                            double reentryTheta = reentryResult.Value.theta;

                            // Найдена точка входа - обходим контур от точки выхода к точке входа
                            FollowContourToReentry(
                                op, exitPoint.Value, reentryPoint, contourPoints,
                                addLine, g1, fmt, culture);
                            currentPos = reentryPoint;
                            wasInside = true;
                            exitPoint = null;
                            
                            // Продолжаем спираль с угла точки входа, чтобы не пропустить витки
                            // Устанавливаем prevTheta на угол точки входа, а θ на следующую точку
                            prevTheta = reentryTheta;
                            θ = reentryTheta + stepAngle;
                            // Переходим к следующей итерации - θ уже установлен правильно
                            continue;
                        }
                        else
                        {
                            // Точки входа нет - точка выхода последняя
                            // Обходим контур полностью и возвращаемся в центр
                            FollowContourFull(
                                op, exitPoint.Value, contourPoints, addLine, g1, fmt, culture);
                            // Возвращаемся в центр без подъема инструмента
                            addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                            finished = true;
                        }
                    }
                    else
                    {
                        // Не удалось найти пересечение - пропускаем точку
                        wasInside = false;
                    }
                }
                else if (!isInside && !wasInside)
                {
                    // Обе точки снаружи - пропускаем, но увеличиваем угол
                    prevTheta = θ;
                    θ += stepAngle;
                    continue;
                }
                else if (isInside && !wasInside)
                {
                    // Вернулись внутрь - это точка входа (не должно происходить, так как обрабатывается выше)
                    addLine($"{g1} X{nextX.ToString(fmt, culture)} Y{nextY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    currentPos = nextPos;
                    wasInside = true;
                    exitPoint = null;
                    prevTheta = θ;
                    θ += stepAngle;
                }
            }

            // Если спираль закончилась, но мы все еще внутри, обходим контур полностью
            if (!finished && wasInside && exitPoint == null)
            {
                // Находим ближайшую точку контура к текущей позиции
                int closestIndex = FindClosestContourPoint(currentPos, contourPoints);
                FollowContourFromPoint(
                    op, closestIndex, contourPoints, addLine, g1, fmt, culture);
                // Возвращаемся в центр без подъема инструмента
                addLine($"{g1} X{center.x.ToString(fmt, culture)} Y{center.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Находит точку пересечения сегмента спирали с контуром и возвращает точный угол спирали в этой точке.
        /// </summary>
        private ((double x, double y) point, double theta)? FindExitPointWithTheta(
            (double x, double y) start,
            (double x, double y) end,
            double startTheta,
            double endTheta,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            double a,
            double b,
            double dirSign,
            double tolerance)
        {
            // Проверяем пересечение сегмента спирали с каждым сегментом контура
            for (int i = 0; i < contourPoints.Count; i++)
            {
                var p1 = contourPoints[i];
                var p2 = contourPoints[(i + 1) % contourPoints.Count];

                var intersection = FindLineSegmentIntersection(
                    start.x, start.y, end.x, end.y,
                    p1.x, p1.y, p2.x, p2.y,
                    tolerance);

                if (intersection.HasValue)
                {
                    // Вычисляем точный угол для точки пересечения
                    // Используем интерполяцию между startTheta и endTheta
                    double dx = end.x - start.x;
                    double dy = end.y - start.y;
                    double segLen = Math.Sqrt(dx * dx + dy * dy);
                    
                    double t = 0.5; // Начальное приближение
                    if (segLen > tolerance)
                    {
                        double dxInt = intersection.Value.x - start.x;
                        double dyInt = intersection.Value.y - start.y;
                        t = (dxInt * dx + dyInt * dy) / (segLen * segLen);
                        t = Math.Max(0, Math.Min(1, t));
                    }
                    
                    double intersectionTheta = startTheta + t * (endTheta - startTheta);
                    return (intersection.Value, intersectionTheta);
                }
            }

            return null;
        }

        /// <summary>
        /// Находит точку пересечения сегмента спирали с контуром.
        /// </summary>
        private (double x, double y)? FindSpiralContourIntersection(
            (double x, double y) start,
            (double x, double y) end,
            List<(double x, double y)> contourPoints,
            double tolerance)
        {
            // Проверяем пересечение сегмента спирали с каждым сегментом контура
            for (int i = 0; i < contourPoints.Count; i++)
            {
                var p1 = contourPoints[i];
                var p2 = contourPoints[(i + 1) % contourPoints.Count];

                var intersection = FindLineSegmentIntersection(
                    start.x, start.y, end.x, end.y,
                    p1.x, p1.y, p2.x, p2.y,
                    tolerance);

                if (intersection.HasValue)
                {
                    return intersection.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Находит точку повторного входа спирали в контур после выхода.
        /// Возвращает точку пересечения спирали с контуром (точка Б) и угол спирали в этой точке.
        /// </summary>
        private ((double x, double y) point, double theta)? FindReentryPointWithTheta(
            (double x, double y) exitPoint,
            double currentTheta,
            double maxTheta,
            double stepAngle,
            double dirSign,
            (double x, double y) center,
            double a,
            double b,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            List<(double x, double y)> contourPoints,
            double tolerance)
        {
            // Продолжаем спираль после точки выхода и ищем точку повторного входа
            (double x, double y)? prevPos = null;
            double prevTheta = currentTheta;
            bool wasOutside = true;

            for (double θ = currentTheta + stepAngle; θ <= maxTheta; θ += stepAngle)
            {
                double r = a + b * θ;
                double ang = θ * dirSign;
                double x = center.x + r * Math.Cos(ang);
                double y = center.y + r * Math.Sin(ang);
                (double x, double y) currentPos = (x, y);

                // Проверяем, находится ли точка внутри контура
                bool isInside = geometry.IsPointInside(x, y, toolRadius, taperOffset);

                if (isInside && wasOutside && prevPos.HasValue)
                {
                    // Пересекли контур - вернулись внутрь
                    // Находим точку пересечения спирали с контуром
                    var intersection = FindSpiralContourIntersection(
                        prevPos.Value, currentPos, contourPoints, tolerance);

                    if (intersection.HasValue)
                    {
                        // Вычисляем точный угол для точки пересечения
                        // Используем интерполяцию между prevTheta и θ
                        double t = 0.5; // Начальное приближение - середина сегмента
                        
                        // Уточняем параметр t для точки пересечения
                        double dx = currentPos.x - prevPos.Value.x;
                        double dy = currentPos.y - prevPos.Value.y;
                        double segLen = Math.Sqrt(dx * dx + dy * dy);
                        if (segLen > tolerance)
                        {
                            double dxInt = intersection.Value.x - prevPos.Value.x;
                            double dyInt = intersection.Value.y - prevPos.Value.y;
                            t = (dxInt * dx + dyInt * dy) / (segLen * segLen);
                            t = Math.Max(0, Math.Min(1, t));
                        }
                        
                        double intersectionTheta = prevTheta + t * (θ - prevTheta);
                        return (intersection.Value, intersectionTheta);
                    }
                }

                prevPos = currentPos;
                prevTheta = θ;
                wasOutside = !isInside;
            }

            return null;
        }

        /// <summary>
        /// Обходит контур от точки выхода к точке повторного входа.
        /// Точки А и Б должны лежать на контуре.
        /// Движение строго по контуру через все вершины.
        /// </summary>
        private void FollowContourToReentry(
            IPocketOperation op,
            (double x, double y) exitPoint,
            (double x, double y) reentryPoint,
            List<(double x, double y)> contourPoints,
            Action<string> addLine,
            string g1,
            string fmt,
            CultureInfo culture)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            double tolerance = 1e-6;

            // Находим сегменты контура, на которых находятся точки выхода и входа
            var exitSegment = FindContourSegment(exitPoint, contourPoints, tolerance);
            var reentrySegment = FindContourSegment(reentryPoint, contourPoints, tolerance);

            if (!exitSegment.HasValue || !reentrySegment.HasValue)
                return;

            int exitSegIndex = exitSegment.Value.segmentIndex;
            int reentrySegIndex = reentrySegment.Value.segmentIndex;
            (double x, double y) exitOnContour = exitSegment.Value.pointOnContour;
            (double x, double y) reentryOnContour = reentrySegment.Value.pointOnContour;

            // Определяем направление обхода в зависимости от настроек
            bool clockwise = op.Direction == MillingDirection.Clockwise;
            int step = clockwise ? -1 : 1;

            // Начинаем с точки выхода на контуре
            addLine($"{g1} X{exitOnContour.x.ToString(fmt, culture)} Y{exitOnContour.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            // Если точки на одном сегменте, просто идем от одной к другой по сегменту
            if (exitSegIndex == reentrySegIndex)
            {
                addLine($"{g1} X{reentryOnContour.x.ToString(fmt, culture)} Y{reentryOnContour.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                return;
            }

            // Идем строго по контуру от точки выхода к точке входа
            // Сначала доходим до конца сегмента с точкой выхода (до следующей вершины)
            int nextVertexIndex = (exitSegIndex + 1) % contourPoints.Count;
            var nextVertex = contourPoints[nextVertexIndex];
            addLine($"{g1} X{nextVertex.x.ToString(fmt, culture)} Y{nextVertex.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            // Теперь идем по вершинам контура до сегмента с точкой входа
            int currentIndex = nextVertexIndex;
            int visited = 0;
            int maxVisits = contourPoints.Count;

            while (visited < maxVisits)
            {
                // Если достигли начала сегмента с точкой входа, идем к точке входа
                if (currentIndex == reentrySegIndex)
                {
                    addLine($"{g1} X{reentryOnContour.x.ToString(fmt, culture)} Y{reentryOnContour.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    break;
                }

                // Переходим к следующей вершине контура
                currentIndex = (currentIndex + step + contourPoints.Count) % contourPoints.Count;
                var point = contourPoints[currentIndex];
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                visited++;
            }
        }

        /// <summary>
        /// Находит сегмент контура, на котором находится заданная точка, и возвращает точку на этом сегменте.
        /// </summary>
        private ((double x, double y) pointOnContour, int segmentIndex)? FindContourSegment(
            (double x, double y) point,
            List<(double x, double y)> contourPoints,
            double tolerance)
        {
            if (contourPoints == null || contourPoints.Count < 2)
                return null;

            double minDist = double.MaxValue;
            (double x, double y)? closestPoint = null;
            int closestSegmentIndex = -1;

            for (int i = 0; i < contourPoints.Count; i++)
            {
                var p1 = contourPoints[i];
                var p2 = contourPoints[(i + 1) % contourPoints.Count];

                // Проецируем точку на сегмент контура
                var projection = ProjectPointToSegment(point, p1, p2);
                if (projection.HasValue)
                {
                    double dx = projection.Value.x - point.x;
                    double dy = projection.Value.y - point.y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < minDist && dist < tolerance * 10) // Увеличенный допуск для поиска
                    {
                        minDist = dist;
                        closestPoint = projection.Value;
                        closestSegmentIndex = i;
                    }
                }
            }

            if (closestPoint.HasValue && closestSegmentIndex >= 0)
            {
                return (closestPoint.Value, closestSegmentIndex);
            }

            return null;
        }

        /// <summary>
        /// Проецирует точку на сегмент и возвращает проекцию, если она находится на сегменте.
        /// </summary>
        private (double x, double y)? ProjectPointToSegment(
            (double x, double y) point,
            (double x, double y) segStart,
            (double x, double y) segEnd)
        {
            double dx = segEnd.x - segStart.x;
            double dy = segEnd.y - segStart.y;
            double segLenSq = dx * dx + dy * dy;

            if (segLenSq < 1e-12)
            {
                // Сегмент нулевой длины
                double dist = Math.Sqrt(Math.Pow(point.x - segStart.x, 2) + Math.Pow(point.y - segStart.y, 2));
                if (dist < 1e-6)
                    return segStart;
                return null;
            }

            // Параметр t для проекции: t = 0 в начале сегмента, t = 1 в конце
            double t = ((point.x - segStart.x) * dx + (point.y - segStart.y) * dy) / segLenSq;

            // Если проекция находится на сегменте (с небольшим допуском)
            if (t >= -0.01 && t <= 1.01)
            {
                t = Math.Max(0, Math.Min(1, t));
                return (segStart.x + t * dx, segStart.y + t * dy);
            }

            return null;
        }

        /// <summary>
        /// Обходит контур полностью от точки выхода.
        /// </summary>
        private void FollowContourFull(
            IPocketOperation op,
            (double x, double y) startPoint,
            List<(double x, double y)> contourPoints,
            Action<string> addLine,
            string g1,
            string fmt,
            CultureInfo culture)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            int startIndex = FindClosestContourPoint(startPoint, contourPoints);
            if (startIndex < 0)
                return;

            FollowContourFromPoint(op, startIndex, contourPoints, addLine, g1, fmt, culture);
        }

        /// <summary>
        /// Обходит контур полностью начиная с указанной точки.
        /// </summary>
        private void FollowContourFromPoint(
            IPocketOperation op,
            int startIndex,
            List<(double x, double y)> contourPoints,
            Action<string> addLine,
            string g1,
            string fmt,
            CultureInfo culture)
        {
            if (contourPoints == null || contourPoints.Count == 0 || startIndex < 0)
                return;

            bool clockwise = op.Direction == MillingDirection.Clockwise;
            int step = clockwise ? -1 : 1;

            // Обходим контур полностью
            for (int i = 0; i <= contourPoints.Count; i++)
            {
                int idx = (startIndex + i * step + contourPoints.Count) % contourPoints.Count;
                var point = contourPoints[idx];
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Находит ближайшую точку контура к заданной точке.
        /// </summary>
        private int FindClosestContourPoint(
            (double x, double y) point,
            List<(double x, double y)> contourPoints)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return -1;

            int closestIndex = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < contourPoints.Count; i++)
            {
                double dx = contourPoints[i].x - point.x;
                double dy = contourPoints[i].y - point.y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        /// <summary>
        /// Вычисляет угол для точки относительно центра спирали.
        /// </summary>
        private double CalculateAngleFromCenter(
            (double x, double y) point,
            (double x, double y) center,
            double a,
            double b)
        {
            double dx = point.x - center.x;
            double dy = point.y - center.y;
            double r = Math.Sqrt(dx * dx + dy * dy);
            
            if (r <= 0)
                return 0;

            // Из формулы r = a + b*θ находим θ
            double theta = (r - a) / b;
            return Math.Max(0, theta);
        }

        /// <summary>
        /// Находит точку пересечения двух отрезков.
        /// </summary>
        private (double x, double y)? FindLineSegmentIntersection(
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
                return (x1 + t1 * dx1, y1 + t1 * dy1);
            }

            return null;
        }

        /// <summary>
        /// Генерирует обход эквидистантного контура (упрощенная версия вместо спирали).
        /// </summary>
        private void GenerateEquidistantContour(
            IPocketOperation op,
            List<(double x, double y)> contourPoints,
            Action<string> addLine,
            string g1,
            string fmt,
            CultureInfo culture)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            // Просто обходим контур по порядку
            foreach (var point in contourPoints)
            {
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }

            // Замыкаем контур, если он не замкнут
            if (contourPoints.Count > 0)
            {
                var firstPoint = contourPoints[0];
                var lastPoint = contourPoints[contourPoints.Count - 1];
                double tolerance = 1e-6;
                double dx = firstPoint.x - lastPoint.x;
                double dy = firstPoint.y - lastPoint.y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > tolerance)
                {
                    addLine($"{g1} X{firstPoint.x.ToString(fmt, culture)} Y{firstPoint.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
            }
        }

        // Временно удалено: GenerateWallsFinishing - будет реализовано заново

        /// <summary>
        /// Клонирует операцию кармана.
        /// </summary>
        private T CloneOperation<T>(T source) where T : IPocketOperation
        {
            if (source == null)
                return default(T);

            // Клонируем в зависимости от конкретного типа
            if (source is PocketCircleOperation circleOp)
            {
                return (T)(IPocketOperation)new PocketCircleOperation
                {
                    Name = circleOp.Name,
                    IsEnabled = circleOp.IsEnabled,
                    PocketStrategy = circleOp.PocketStrategy,
                    Direction = circleOp.Direction,
                    CenterX = circleOp.CenterX,
                    CenterY = circleOp.CenterY,
                    Radius = circleOp.Radius,
                    TotalDepth = circleOp.TotalDepth,
                    StepDepth = circleOp.StepDepth,
                    ToolDiameter = circleOp.ToolDiameter,
                    ContourHeight = circleOp.ContourHeight,
                    FeedXYRapid = circleOp.FeedXYRapid,
                    FeedXYWork = circleOp.FeedXYWork,
                    FeedZRapid = circleOp.FeedZRapid,
                    FeedZWork = circleOp.FeedZWork,
                    SafeZHeight = circleOp.SafeZHeight,
                    RetractHeight = circleOp.RetractHeight,
                    StepPercentOfTool = circleOp.StepPercentOfTool,
                    Decimals = circleOp.Decimals,
                    LineAngleDeg = circleOp.LineAngleDeg,
                    WallTaperAngleDeg = circleOp.WallTaperAngleDeg,
                    IsRoughingEnabled = circleOp.IsRoughingEnabled,
                    IsFinishingEnabled = circleOp.IsFinishingEnabled,
                    FinishAllowance = circleOp.FinishAllowance,
                    FinishingMode = circleOp.FinishingMode,
                    Metadata = circleOp.Metadata != null ? new Dictionary<string, object>(circleOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketRectangleOperation rectOp)
            {
                return (T)(IPocketOperation)new PocketRectangleOperation
                {
                    Name = rectOp.Name,
                    IsEnabled = rectOp.IsEnabled,
                    PocketStrategy = rectOp.PocketStrategy,
                    Direction = rectOp.Direction,
                    Width = rectOp.Width,
                    Height = rectOp.Height,
                    RotationAngle = rectOp.RotationAngle,
                    TotalDepth = rectOp.TotalDepth,
                    StepDepth = rectOp.StepDepth,
                    ToolDiameter = rectOp.ToolDiameter,
                    ContourHeight = rectOp.ContourHeight,
                    FeedXYRapid = rectOp.FeedXYRapid,
                    FeedXYWork = rectOp.FeedXYWork,
                    FeedZRapid = rectOp.FeedZRapid,
                    FeedZWork = rectOp.FeedZWork,
                    SafeZHeight = rectOp.SafeZHeight,
                    RetractHeight = rectOp.RetractHeight,
                    ReferencePointX = rectOp.ReferencePointX,
                    ReferencePointY = rectOp.ReferencePointY,
                    ReferencePointType = rectOp.ReferencePointType,
                    StepPercentOfTool = rectOp.StepPercentOfTool,
                    Decimals = rectOp.Decimals,
                    LineAngleDeg = rectOp.LineAngleDeg,
                    WallTaperAngleDeg = rectOp.WallTaperAngleDeg,
                    IsRoughingEnabled = rectOp.IsRoughingEnabled,
                    IsFinishingEnabled = rectOp.IsFinishingEnabled,
                    FinishAllowance = rectOp.FinishAllowance,
                    FinishingMode = rectOp.FinishingMode,
                    Metadata = rectOp.Metadata != null ? new Dictionary<string, object>(rectOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketEllipseOperation ellipseOp)
            {
                return (T)(IPocketOperation)new PocketEllipseOperation
                {
                    Name = ellipseOp.Name,
                    IsEnabled = ellipseOp.IsEnabled,
                    PocketStrategy = ellipseOp.PocketStrategy,
                    Direction = ellipseOp.Direction,
                    CenterX = ellipseOp.CenterX,
                    CenterY = ellipseOp.CenterY,
                    RadiusX = ellipseOp.RadiusX,
                    RadiusY = ellipseOp.RadiusY,
                    RotationAngle = ellipseOp.RotationAngle,
                    TotalDepth = ellipseOp.TotalDepth,
                    StepDepth = ellipseOp.StepDepth,
                    ToolDiameter = ellipseOp.ToolDiameter,
                    ContourHeight = ellipseOp.ContourHeight,
                    FeedXYRapid = ellipseOp.FeedXYRapid,
                    FeedXYWork = ellipseOp.FeedXYWork,
                    FeedZRapid = ellipseOp.FeedZRapid,
                    FeedZWork = ellipseOp.FeedZWork,
                    SafeZHeight = ellipseOp.SafeZHeight,
                    RetractHeight = ellipseOp.RetractHeight,
                    StepPercentOfTool = ellipseOp.StepPercentOfTool,
                    Decimals = ellipseOp.Decimals,
                    LineAngleDeg = ellipseOp.LineAngleDeg,
                    WallTaperAngleDeg = ellipseOp.WallTaperAngleDeg,
                    IsRoughingEnabled = ellipseOp.IsRoughingEnabled,
                    IsFinishingEnabled = ellipseOp.IsFinishingEnabled,
                    FinishAllowance = ellipseOp.FinishAllowance,
                    FinishingMode = ellipseOp.FinishingMode,
                    Metadata = ellipseOp.Metadata != null ? new Dictionary<string, object>(ellipseOp.Metadata) : new Dictionary<string, object>()
                };
            }

            if (source is PocketDxfOperation dxfOp)
            {
                var cloned = new PocketDxfOperation
                {
                    Name = dxfOp.Name,
                    IsEnabled = dxfOp.IsEnabled,
                    PocketStrategy = dxfOp.PocketStrategy,
                    Direction = dxfOp.Direction,
                    TotalDepth = dxfOp.TotalDepth,
                    StepDepth = dxfOp.StepDepth,
                    ToolDiameter = dxfOp.ToolDiameter,
                    ContourHeight = dxfOp.ContourHeight,
                    FeedXYRapid = dxfOp.FeedXYRapid,
                    FeedXYWork = dxfOp.FeedXYWork,
                    FeedZRapid = dxfOp.FeedZRapid,
                    FeedZWork = dxfOp.FeedZWork,
                    SafeZHeight = dxfOp.SafeZHeight,
                    RetractHeight = dxfOp.RetractHeight,
                    StepPercentOfTool = dxfOp.StepPercentOfTool,
                    Decimals = dxfOp.Decimals,
                    LineAngleDeg = dxfOp.LineAngleDeg,
                    WallTaperAngleDeg = dxfOp.WallTaperAngleDeg,
                    IsRoughingEnabled = dxfOp.IsRoughingEnabled,
                    IsFinishingEnabled = dxfOp.IsFinishingEnabled,
                    FinishAllowance = dxfOp.FinishAllowance,
                    FinishingMode = dxfOp.FinishingMode,
                    DxfFilePath = dxfOp.DxfFilePath,
                    Metadata = dxfOp.Metadata != null ? new Dictionary<string, object>(dxfOp.Metadata) : new Dictionary<string, object>()
                };

                // Клонируем контуры
                if (dxfOp.ClosedContours != null)
                {
                    cloned.ClosedContours = new List<DxfPolyline>();
                    foreach (var contour in dxfOp.ClosedContours)
                    {
                        if (contour?.Points != null)
                        {
                            var clonedContour = new DxfPolyline
                            {
                                Points = new List<DxfPoint>()
                            };
                            foreach (var point in contour.Points)
                            {
                                clonedContour.Points.Add(new DxfPoint { X = point.X, Y = point.Y });
                            }
                            cloned.ClosedContours.Add(clonedContour);
                        }
                    }
                }

                return (T)(IPocketOperation)cloned;
            }

            throw new NotSupportedException($"Unsupported pocket operation type: {source.GetType().Name}");
        }

        // Временно удалено: ApplyRoughingAllowance - будет реализовано заново

        /// <summary>
        /// Проверяет, не стал ли карман слишком маленьким.
        /// </summary>
        private bool IsOperationTooSmall<T>(T op) where T : IPocketOperation
        {
            if (op == null)
                return true;

            double toolRadius = op.ToolDiameter / 2.0;
            double minSize = toolRadius * 2.1; // Минимальный размер должен быть больше диаметра инструмента

            if (op is PocketCircleOperation circleOp)
            {
                return circleOp.Radius <= minSize;
            }
            else if (op is PocketRectangleOperation rectOp)
            {
                return rectOp.Width <= minSize || rectOp.Height <= minSize;
            }
            else if (op is PocketEllipseOperation ellipseOp)
            {
                return ellipseOp.RadiusX <= minSize || ellipseOp.RadiusY <= minSize;
            }
            else if (op is PocketDxfOperation dxfOp)
            {
                // Для DXF проверяем, что есть хотя бы один контур
                return dxfOp.ClosedContours == null || dxfOp.ClosedContours.Count == 0;
            }

            return false;
        }

        // Временно удалено: ApplyBottomFinishingAllowance - будет реализовано заново
    }
}


