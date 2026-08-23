using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Генератор слоя DXF-кармана с несколькими контурами (пункт 4.6 плана).
    /// Перенесён из UnifiedPocketGenerator.GenerateDxfLayerWithSpiral
    /// без изменения поведения: по каждому контуру — смещённый контур,
    /// эвристики отсечки (<see cref="ContourCutoffAnalyzer"/>), Z-переходы
    /// и обработка контура стратегией (<see cref="IPocketPocketingStrategy"/>).
    /// </summary>
    public sealed class DxfPocketLayerGenerator
    {
        private readonly IPocketPocketingStrategy _strategy;

        public DxfPocketLayerGenerator(IPocketPocketingStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        /// <summary>
        /// Генерирует один слой для DXF кармана с несколькими контурами.
        /// </summary>
        /// <returns>true, если хотя бы один контур был обработан и обработку нужно продолжить; false, если все контуры пропущены</returns>
        public bool GenerateLayer(
            PocketDxfOperation op,
            double toolRadius,
            double taperOffset,
            double step,
            double currentZ,
            double nextZ,
            int passNumber,
            ContourCutoffAnalyzer cutoff,
            ProgramBuilder builder,
            GCodeSettings settings)
        {
            int decimals = op.Decimals;

            if (op.ClosedContours == null || op.ClosedContours.Count == 0)
                return false;

            bool isFirstContour = true;
            bool atLeastOneContourProcessed = false;

            for (int contourIndex = 0; contourIndex < op.ClosedContours.Count; contourIndex++)
            {
                var contour = op.ClosedContours[contourIndex];
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

                // Создаем геометрию для этого контура
                var geometry = new DxfPocketGeometry(op, contour);

                // Получаем эквидистантный контур (смещенный внутрь) для вычисления площади
                var offsetContour = geometry.GetContour(toolRadius, taperOffset);
                if (offsetContour == null)
                {
                    cutoff.RecordMissingContour(contourIndex, passNumber);
                    continue;
                }

                // Вычисляем площадь текущего слоя
                double currentArea = offsetContour.GetArea();

                // Проверяем все критерии отсечки (площади, «песочные часы»,
                // обход, векторы, размер)
                if (cutoff.ShouldSkip(
                        contourIndex,
                        currentArea,
                        passNumber,
                        op.WallTaperAngleDeg,
                        geometry.HasWindingDirectionChanged(toolRadius, taperOffset),
                        geometry.HasVectorDirectionChanged(toolRadius, taperOffset),
                        geometry.IsContourTooSmall(toolRadius, taperOffset)))
                {
                    // Этот контур достиг своего последнего слоя или слишком маленький -
                    // пропускаем его, но продолжаем обрабатывать остальные контуры
                    continue;
                }

                var contourPoints = offsetContour.GetPoints().ToList();
                if (contourPoints.Count == 0)
                    continue;

                // Вычисляем геометрический центр контура
                var center = geometry.GetCenter();

                // Поднимаем инструмент перед переходом к следующему контуру (кроме первого)
                if (!isFirstContour)
                {
                    builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);
                }

                // Перемещаемся к центру контура
                builder.RapidTo(x: center.x, y: center.y, feed: op.FeedXYRapid, decimals: decimals);

                // Опускаемся на рабочую высоту (только для первого контура, для остальных уже на нужной высоте)
                if (isFirstContour)
                {
                    builder.RapidTo(z: currentZ, feed: op.FeedZRapid, decimals: decimals);
                    builder.LinearTo(z: nextZ, feed: op.FeedZWork, decimals: decimals);
                }
                else
                {
                    builder.RapidTo(z: nextZ, feed: op.FeedZRapid, decimals: decimals);
                }

                // Генерируем обработку этого контура стратегией
                _strategy.MillContour(op, geometry, toolRadius, taperOffset, step, contourPoints, center, builder, settings);

                // Возврат в центр контура и подъем
                builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
                builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);

                // Сохраняем площадь текущего слоя для следующей итерации
                cutoff.RecordMilled(contourIndex, currentArea);

                isFirstContour = false;
                atLeastOneContourProcessed = true;
            }

            // Возвращаем true, если хотя бы один контур был обработан
            // Это означает, что нужно продолжить обработку следующих слоев
            return atLeastOneContourProcessed;
        }
    }
}
