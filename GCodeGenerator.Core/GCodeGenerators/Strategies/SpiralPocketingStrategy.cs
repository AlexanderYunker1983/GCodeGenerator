using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Спиральная стратегия обработки кармана (пункт 4.6 плана).
    /// Перенесена из UnifiedPocketGenerator без изменения поведения:
    /// спираль r = b*θ от центра с контролем выхода за контур
    /// (точка выхода → обход контура → точка повторного входа).
    /// </summary>
    public sealed class SpiralPocketingStrategy : IPocketPocketingStrategy
    {
        /// <summary>
        /// На сколько проекция точки может выходить за пределы стороны
        /// контура и всё ещё считаться лежащей на ней: точка приходит из
        /// пересечения и несёт погрешность вычисления.
        /// </summary>
        private const double BoundsTolerance = 0.01;

        public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
        {
            var op = layer.Operation;
            // Спираль работает на рабочей Z без отводов — workingZ не используется.
            int decimals = op.Decimals;

            if (layer.ContourPoints == null || layer.ContourPoints.Count == 0)
                return;

            // Находим максимальное расстояние от центра до контура
            double maxDistance = 0.0;
            foreach (var point in layer.ContourPoints)
            {
                double dx = point.x - layer.Center.x;
                double dy = point.y - layer.Center.y;
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
            double b = layer.Step / (2.0 * Math.PI);

            // Направление спирали
            double dirSign = op.Direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            // Максимальный угол для достижения внешнего радиуса
            double θMax = maxDistance / b;

            // Количество точек на оборот для плавности
            int pointsPerRevolution = 128;
            double stepAngle = 2.0 * Math.PI / pointsPerRevolution;
            const double tolerance = GeometryTolerances.Vertex;

            // Начинаем с центра
            (double x, double y) currentPos = layer.Center;
            builder.LinearTo(x: currentPos.x, y: currentPos.y, feed: op.FeedXYWork, decimals: decimals);

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
                double nextX = layer.Center.x + r * Math.Cos(ang);
                double nextY = layer.Center.y + r * Math.Sin(ang);
                (double x, double y) nextPos = (nextX, nextY);

                // Проверяем, находится ли следующая точка внутри контура
                bool isInside = layer.Geometry.IsPointInside(nextX, nextY, layer.ContourOffset, layer.TaperOffset);

                if (isInside && wasInside)
                {
                    // Обе точки внутри - просто добавляем точку
                    builder.LinearTo(x: nextX, y: nextY, feed: op.FeedXYWork, decimals: decimals);
                    currentPos = nextPos;
                    prevTheta = θ;
                    θ += stepAngle; // Переходим к следующей точке
                }
                else if (!isInside && wasInside)
                {
                    // Пересекли контур - вышли наружу
                    // Находим точку пересечения и точный угол в этой точке
                    var exitResult = FindExitPointWithTheta(
                        currentPos, nextPos, prevTheta, θ, layer.ContourPoints, layer.Center, a, b, dirSign, tolerance);

                    if (exitResult.HasValue)
                    {
                        exitPoint = exitResult.Value.point;
                        double exitTheta = exitResult.Value.theta;

                        builder.LinearTo(x: exitPoint.Value.x, y: exitPoint.Value.y, feed: op.FeedXYWork, decimals: decimals);
                        currentPos = exitPoint.Value;

                        // Ищем точку повторного входа с сохранением угла
                        var reentryResult = FindReentryPointWithTheta(
                            exitPoint.Value, exitTheta, θMax, stepAngle, dirSign, layer.Center, a, b,
                            layer.Geometry, layer.ContourOffset, layer.TaperOffset, layer.ContourPoints, tolerance);

                        if (reentryResult.HasValue)
                        {
                            var reentryPoint = reentryResult.Value.point;
                            double reentryTheta = reentryResult.Value.theta;

                            // Найдена точка входа - обходим контур от точки выхода к точке входа
                            FollowContourToReentry(
                                op, exitPoint.Value, reentryPoint, layer.ContourPoints,
                                builder, decimals);
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
                                op, exitPoint.Value, layer.ContourPoints, builder, decimals);
                            // Возвращаемся в центр без подъема инструмента
                            builder.LinearTo(x: layer.Center.x, y: layer.Center.y, feed: op.FeedXYWork, decimals: decimals);
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
                    builder.LinearTo(x: nextX, y: nextY, feed: op.FeedXYWork, decimals: decimals);
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
                int closestIndex = Geometry2D.ClosestVertexIndex(
                    layer.ContourPoints, currentPos.x, currentPos.y);
                FollowContourFromPoint(
                    op, closestIndex, layer.ContourPoints, builder, decimals);
                // Возвращаемся в центр без подъема инструмента
                builder.LinearTo(x: layer.Center.x, y: layer.Center.y, feed: op.FeedXYWork, decimals: decimals);
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

                var intersection = Geometry2D.SegmentIntersection(
                    start.x, start.y, end.x, end.y,
                    p1.x, p1.y, p2.x, p2.y,
                    tolerance, tolerance);

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

                var intersection = Geometry2D.SegmentIntersection(
                    start.x, start.y, end.x, end.y,
                    p1.x, p1.y, p2.x, p2.y,
                    tolerance, tolerance);

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
            ToolPathBuilder builder,
            int decimals)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            const double tolerance = GeometryTolerances.Vertex;

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
            builder.LinearTo(x: exitOnContour.x, y: exitOnContour.y, feed: op.FeedXYWork, decimals: decimals);

            // Если точки на одном сегменте, просто идем от одной к другой по сегменту
            if (exitSegIndex == reentrySegIndex)
            {
                builder.LinearTo(x: reentryOnContour.x, y: reentryOnContour.y, feed: op.FeedXYWork, decimals: decimals);
                return;
            }

            // Идем строго по контуру от точки выхода к точке входа
            // Сначала доходим до конца сегмента с точкой выхода (до следующей вершины)
            int nextVertexIndex = (exitSegIndex + 1) % contourPoints.Count;
            var nextVertex = contourPoints[nextVertexIndex];
            builder.LinearTo(x: nextVertex.x, y: nextVertex.y, feed: op.FeedXYWork, decimals: decimals);

            // Теперь идем по вершинам контура до сегмента с точкой входа
            int currentIndex = nextVertexIndex;
            int visited = 0;
            int maxVisits = contourPoints.Count;

            while (visited < maxVisits)
            {
                // Если достигли начала сегмента с точкой входа, идем к точке входа
                if (currentIndex == reentrySegIndex)
                {
                    builder.LinearTo(x: reentryOnContour.x, y: reentryOnContour.y, feed: op.FeedXYWork, decimals: decimals);
                    break;
                }

                // Переходим к следующей вершине контура
                currentIndex = (currentIndex + step + contourPoints.Count) % contourPoints.Count;
                var point = contourPoints[currentIndex];
                builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork, decimals: decimals);

                visited++;
            }
        }

        /// <summary>
        /// Сторона контура, на которой лежит точка, и сама точка на ней.
        ///
        /// Точка приходит из пересечения спирали с контуром, поэтому лежит
        /// на нём лишь с точностью до погрешности: обход контура должен
        /// начинаться с настоящей его точки, а не рядом.
        /// </summary>
        private static ((double x, double y) pointOnContour, int segmentIndex)? FindContourSegment(
            (double x, double y) point,
            List<(double x, double y)> contourPoints,
            double tolerance)
            => Geometry2D.ClosestPointOnClosedPolyline(
                contourPoints, point.x, point.y, BoundsTolerance, tolerance);


        /// <summary>
        /// Обходит контур полностью от точки выхода.
        /// </summary>
        private void FollowContourFull(
            IPocketOperation op,
            (double x, double y) startPoint,
            List<(double x, double y)> contourPoints,
            ToolPathBuilder builder,
            int decimals)
        {
            if (contourPoints == null || contourPoints.Count == 0)
                return;

            int startIndex = Geometry2D.ClosestVertexIndex(contourPoints, startPoint.x, startPoint.y);
            if (startIndex < 0)
                return;

            FollowContourFromPoint(op, startIndex, contourPoints, builder, decimals);
        }

        /// <summary>
        /// Обходит контур полностью начиная с указанной точки.
        /// </summary>
        private void FollowContourFromPoint(
            IPocketOperation op,
            int startIndex,
            List<(double x, double y)> contourPoints,
            ToolPathBuilder builder,
            int decimals)
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
                builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork, decimals: decimals);
            }
        }
    }
}
