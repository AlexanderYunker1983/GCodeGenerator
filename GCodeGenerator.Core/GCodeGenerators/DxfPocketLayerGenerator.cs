using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;

using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Генератор слоя DXF-кармана с несколькими контурами (пункт 4.6 плана).
    ///
    /// Каждый замкнутый контур операции смещается внутрь на радиус инструмента
    /// с учётом уклона стенок. Смещение может дать несколько областей — узкая
    /// перемычка исчезает раньше остального кармана, — и каждая такая область
    /// фрезеруется как самостоятельный карман: подход на безопасной высоте,
    /// врезание, обработка стратегией, подъём.
    ///
    /// Эвристик отсечки слоёв здесь больше нет: пока смещение даёт хотя бы одну
    /// область, слой обрабатывается; как только областей не остаётся — карман
    /// на этой глубине уже, чем инструмент, и цикл по слоям останавливается.
    /// </summary>
    public sealed class DxfPocketLayerGenerator
    {
        /// <summary>
        /// Генерирует один слой для DXF кармана с несколькими контурами.
        /// </summary>
        /// <param name="op">Операция DXF-кармана (замкнутые контуры, подачи, Decimals).</param>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок на глубине слоя.</param>
        /// <param name="step">Шаг обработки.</param>
        /// <param name="currentZ">Z верха слоя.</param>
        /// <param name="nextZ">Рабочая Z слоя.</param>
        /// <param name="strategy">Стратегия обработки (выбирается по <c>op.PocketStrategy</c>, пункт 5.1).</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <returns>true, если хотя бы одна область была обработана и обработку нужно продолжить; false, если областей не осталось</returns>
        public bool GenerateLayer(
            PocketDxfOperation op,
            double toolRadius,
            double taperOffset,
            double step,
            double currentZ,
            double nextZ,
            IPocketPocketingStrategy strategy,
            ToolPathBuilder builder,
            GCodeSettings settings)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            int decimals = op.Decimals;

            if (op.ClosedContours == null || op.ClosedContours.Count == 0)
                return false;

            bool isFirstArea = true;
            bool atLeastOneAreaProcessed = false;

            foreach (var contour in op.ClosedContours)
            {
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

                var sourceGeometry = new DxfPocketGeometry(op, contour);
                foreach (var area in sourceGeometry.GetOffsetParts(toolRadius, taperOffset))
                {
                    if (area.Count < 3)
                        continue;

                    // Область уже смещена на радиус инструмента, поэтому дальше
                    // она обрабатывается как готовая траектория центра фрезы:
                    // стратегия получает нулевые радиус и уклон.
                    var areaGeometry = new DxfPocketGeometry(
                        op,
                        new Polyline2D { Points = new List<Point2D>(area) });

                    var contourPoints = new List<(double x, double y)>(area.Count);
                    foreach (var point in area)
                        contourPoints.Add((point.X, point.Y));

                    var center = areaGeometry.GetCenter();

                    // Поднимаем инструмент перед переходом к следующей области (кроме первой)
                    if (!isFirstArea)
                    {
                        builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);
                    }

                    // Перемещаемся к центру области
                    builder.RapidTo(x: center.x, y: center.y, feed: op.FeedXYRapid, decimals: decimals);

                    // Опускаемся на рабочую высоту (для первой области — от верха слоя,
                    // для остальных инструмент уже на безопасной высоте)
                    if (isFirstArea)
                    {
                        builder.RapidTo(z: currentZ, feed: op.FeedZRapid, decimals: decimals);
                        builder.LinearTo(z: nextZ, feed: op.FeedZWork, decimals: decimals);
                    }
                    else
                    {
                        builder.RapidTo(z: nextZ, feed: op.FeedZRapid, decimals: decimals);
                    }

                    // Область уже смещена на радиус инструмента и уклон стенки,
                    // поэтому для неё оба смещения нулевые.
                    strategy.MillContour(
                        new PocketLayerContext(
                            op, areaGeometry, 0, 0, step, nextZ, contourPoints, center, settings),
                        builder);

                    // Возврат в центр области и подъем
                    builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
                    builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);

                    isFirstArea = false;
                    atLeastOneAreaProcessed = true;
                }
            }

            return atLeastOneAreaProcessed;
        }
    }
}
