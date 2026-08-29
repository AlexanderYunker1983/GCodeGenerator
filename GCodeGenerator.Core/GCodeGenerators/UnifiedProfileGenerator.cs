#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;

using GCodeGenerator.Operations;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов профилей.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// </summary>
    public class UnifiedProfileGenerator : IOperationGenerator
    {
        private readonly ProfileGenerationHelper _helper;

        public UnifiedProfileGenerator()
        {
            _helper = new ProfileGenerationHelper();
        }

        public void Generate(
            OperationBase operation,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            // Проверяем, что операция является профилем
            if (!(operation is ProfileOperationBase profileOp))
                return;

            // Создаем геометрию профиля
            var geometry = OperationCatalog.CreateProfileGeometry(operation);
            if (geometry == null)
                return;

            // Вычисляем смещение инструмента
            var toolOffset = GCodeGenerationHelper.CalculateToolOffset(
                profileOp.ToolPathMode,
                profileOp.ToolDiameter);

            // Генерируем цикл по слоям
            _helper.GenerateLayerLoop(
                profileOp,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    profileOp,
                    geometry,
                    toolOffset,
                    currentZ,
                    nextZ,
                    builder,
                    settings),
                builder,
                settings,
                cancellation);
        }

        /// <summary>
        /// Генерирует один слой профиля.
        /// </summary>
        private void GenerateLayer(
            ProfileOperationBase op,
            IProfileGeometry geometry,
            double toolOffset,
            double currentZ,
            double nextZ,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            // Получаем начальную точку контура
            var startPoint = geometry.GetStartPoint(toolOffset);

            // Генерируем вход в материал
            _helper.GenerateEntry(
                op,
                startPoint,
                currentZ,
                nextZ,
                distance => geometry.GetPointOnContour(distance, toolOffset),
                () => geometry.GetPerimeter(toolOffset),
                () => geometry.GetCornerDistances(toolOffset),
                builder,
                settings);

            // После входа мы находимся на начальной точке контура
            // Генерируем путь по контуру
            if (settings.Format.AllowArcs && geometry.SupportsArcs)
            {
                // Используем дуги, если поддерживаются
                var arcSegments = geometry.GetArcSegments(toolOffset).ToList();
                if (arcSegments.Count > 0)
                {
                    GenerateContourWithArcs(op, geometry, toolOffset, arcSegments, builder, settings);
                }
                else
                {
                    // Fallback на точки, если дуги не доступны
                    GenerateContourFromPoints(op, geometry, toolOffset, startPoint, nextZ, builder, settings);
                }
            }
            else
            {
                // Генерируем из точек
                GenerateContourFromPoints(op, geometry, toolOffset, startPoint, nextZ, builder, settings);
            }
        }

        /// <summary>
        /// Генерирует контур из точек.
        /// </summary>
        private void GenerateContourFromPoints(
            ProfileOperationBase op,
            IProfileGeometry geometry,
            double toolOffset,
            (double x, double y) currentPosition,
            double workingZ,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            int decimals = op.Decimals;

            // Готовые контуры геометрия расставляет сама, генератор только
            // добавляет переходы между ними.
            if (geometry.ProvidesOrderedContours)
            {
                GenerateOrderedContours(op, geometry, workingZ, builder);
                return;
            }

            var points = geometry.GetContourPoints(toolOffset, op.Direction).ToList();
            
            if (points.Count == 0)
                return;

            // Удаляем последовательные дубликаты точек из списка
            var cleanedPoints = new List<(double x, double y)>();
            const double tolerance = GeometryTolerances.Vertex;
            
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                
                // Добавляем точку только если она отличается от предыдущей
                if (cleanedPoints.Count == 0 || 
                    Math.Abs(cleanedPoints[cleanedPoints.Count - 1].x - point.x) > tolerance ||
                    Math.Abs(cleanedPoints[cleanedPoints.Count - 1].y - point.y) > tolerance)
                {
                    cleanedPoints.Add(point);
                }
            }
            
            if (cleanedPoints.Count == 0)
                return;

            // Находим ближайшую точку к текущей позиции
            int currentIndex = 0;
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < cleanedPoints.Count; i++)
            {
                double dx = cleanedPoints[i].x - currentPosition.x;
                double dy = cleanedPoints[i].y - currentPosition.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    currentIndex = i;
                }
            }

            // Если мы находимся очень близко к найденной точке, начинаем со следующей
            // Иначе обрабатываем все точки с начала
            int startIndex = (minDistance < tolerance && currentIndex < cleanedPoints.Count - 1) 
                ? currentIndex + 1 
                : 0;

            // Обрабатываем все точки контура последовательно, начиная с startIndex
            for (int i = startIndex; i < cleanedPoints.Count; i++)
            {
                var point = cleanedPoints[i];
                builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);
            }
            
            // Если мы начали не с начала, обрабатываем точки от начала до startIndex
            if (startIndex > 0)
            {
                for (int i = 0; i < startIndex; i++)
                {
                    var point = cleanedPoints[i];
                    builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);
                }
            }
            
            // Замыкаем контур - возвращаемся к первой точке, если она не совпадает с последней
            GCodeGenerationHelper.CloseContour(builder, cleanedPoints, op.FeedXYWork, tolerance);
        }

        /// <summary>
        /// Обходит готовые контуры: точки в них уже расставлены геометрией,
        /// а генератор добавляет переходы — подъём на безопасную высоту,
        /// подход к началу следующего контура и опускание на рабочую Z.
        /// </summary>
        /// <param name="op">Операция профиля.</param>
        /// <param name="geometry">Геометрия, дающая готовые контуры.</param>
        /// <param name="workingZ">Рабочая высота слоя.</param>
        /// <param name="builder">Построитель траектории.</param>
        private static void GenerateOrderedContours(
            ProfileOperationBase op,
            IProfileGeometry geometry,
            double workingZ,
            ToolPathBuilder builder)
        {
            bool isFirstContour = true;

            foreach (var contourPoints in geometry.GetOrderedContours(GeometryTolerances.Vertex))
            {
                if (contourPoints.Count == 0)
                    continue;

                var traversalStart = op.Direction == MillingDirection.Clockwise
                    ? contourPoints[contourPoints.Count - 1]
                    : contourPoints[0];

                if (!isFirstContour)
                {
                    builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);
                    builder.RapidTo(x: traversalStart.x, y: traversalStart.y, feed: op.FeedXYRapid);
                    builder.LinearTo(z: workingZ, feed: op.FeedZWork);
                }

                if (op.Direction == MillingDirection.Clockwise)
                {
                    for (int i = contourPoints.Count - 1; i >= 0; i--)
                    {
                        var point = contourPoints[i];
                        builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);
                    }
                }
                else
                {
                    for (int i = 0; i < contourPoints.Count; i++)
                    {
                        var point = contourPoints[i];
                        builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);
                    }
                }

                if (geometry.IsOrderedContourClosed(contourPoints))
                    builder.LinearTo(x: traversalStart.x, y: traversalStart.y, feed: op.FeedXYWork);

                isFirstContour = false;
            }
        }

        /// <summary>
        /// Генерирует контур с использованием дуг.
        /// </summary>
        private void GenerateContourWithArcs(
            ProfileOperationBase op,
            IProfileGeometry geometry,
            double toolOffset,
            System.Collections.Generic.List<IArcSegment> arcSegments,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            int decimals = op.Decimals;

            foreach (var arc in arcSegments)
            {
                var i = arc.Center.x - arc.StartPoint.x;
                var j = arc.Center.y - arc.StartPoint.y;

                if (arc.IsClockwise)
                    builder.ArcCW(arc.EndPoint.x, arc.EndPoint.y, i, j, op.FeedXYWork);
                else
                    builder.ArcCCW(arc.EndPoint.x, arc.EndPoint.y, i, j, op.FeedXYWork);
            }
        }
    }
}
