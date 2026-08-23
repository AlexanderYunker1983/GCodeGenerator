using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов карманов.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// Пункт 4.6 плана (декомпозиция): слой DXF-кармана — <see cref="DxfPocketLayerGenerator"/>,
    /// эвристики отсечки — <see cref="ContourCutoffAnalyzer"/>,
    /// обработка контура — <see cref="IPocketPocketingStrategy"/>
    /// (сейчас <see cref="SpiralPocketingStrategy"/>; новые стратегии — фаза 5, D1).
    /// </summary>
    public class UnifiedPocketGenerator : IOperationGenerator
    {
        private readonly PocketGenerationHelper _helper;
        private readonly DxfPocketLayerGenerator _dxfLayerGenerator;

        public UnifiedPocketGenerator()
        {
            _helper = new PocketGenerationHelper();
            _dxfLayerGenerator = new DxfPocketLayerGenerator();
        }

        /// <summary>
        /// Выбор стратегии обработки по <c>op.PocketStrategy</c> (пункт 5.1 плана).
        /// Пока не все стратегии реализованы (5.4–5.5), незарегистрированные
        /// значения обрабатываются спиралью.
        /// </summary>
        private static IPocketPocketingStrategy GetStrategy(PocketStrategy strategy)
        {
            switch (strategy)
            {
                case PocketStrategy.Concentric:
                    return new ConcentricPocketingStrategy();
                case PocketStrategy.Radial:
                    return new RadialPocketingStrategy();
                case PocketStrategy.Spiral:
                default:
                    return new SpiralPocketingStrategy();
            }
        }

        public void Generate(OperationBase operation, ProgramBuilder builder, GCodeSettings settings)
        {
            // Проверяем, что операция является карманом
            if (!(operation is IPocketOperation pocketOp))
                return;

            // Создаем геометрию кармана
            var geometry = PocketGeometryFactory.Create(operation);
            if (geometry == null)
                return;

            // Временно: генерируем только основную обработку без roughing/finishing
            GenerateInternal(pocketOp, geometry, builder, settings);
        }

        /// <summary>
        /// Генерирует внутреннюю обработку кармана (без учета rough/finish).
        /// </summary>
        private void GenerateInternal(
            IPocketOperation op,
            IPocketGeometry geometry,
            ProgramBuilder builder,
            GCodeSettings settings)
        {
            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;
            double step = GCodeGenerationHelper.CalculateStep(op.ToolDiameter, stepPercent);

            // Состояние отсечки (площади слоёв, данные подобия) — на вызов Generate
            var cutoff = new ContourCutoffAnalyzer();

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
                    passNumber,
                    cutoff,
                    builder,
                    settings),
                builder,
                settings);
        }

        /// <summary>
        /// Генерирует один слой кармана.
        /// </summary>
        /// <returns>true, если обработку нужно продолжить; false, если контур слишком маленький и обработку нужно прекратить</returns>
        private bool GenerateLayer(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double step,
            double currentZ,
            double nextZ,
            int passNumber,
            ContourCutoffAnalyzer cutoff,
            ProgramBuilder builder,
            GCodeSettings settings)
        {
            int decimals = op.Decimals;

            double depthFromTop = op.ContourHeight - nextZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            // Для DXF операций обрабатываем все контуры отдельно
            // Проверка размера контуров выполняется в DxfPocketLayerGenerator для каждого контура отдельно
            if (op is PocketDxfOperation dxfOp)
            {
                return _dxfLayerGenerator.GenerateLayer(
                    dxfOp, toolRadius, taperOffset, step,
                    currentZ, nextZ, passNumber, cutoff,
                    GetStrategy(op.PocketStrategy), builder, settings);
            }

            // Проверяем, не стал ли контур слишком маленьким для обработки (для не-DXF операций)
            if (geometry.IsContourTooSmall(toolRadius, taperOffset))
            {
                // Контур слишком маленький - прекращаем обработку
                return false;
            }

            // Получаем контур кармана
            var contour = geometry.GetContour(toolRadius, taperOffset);
            if (contour == null)
                return false;

            var center = geometry.GetCenter();
            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return false;

            // Перемещаемся к центру кармана
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);
            builder.RapidTo(x: center.x, y: center.y, feed: op.FeedXYRapid, decimals: decimals);
            builder.RapidTo(z: currentZ, feed: op.FeedZRapid, decimals: decimals);
            builder.LinearTo(z: nextZ, feed: op.FeedZWork, decimals: decimals);

            // Генерируем обработку контура стратегией (выбор по op.PocketStrategy, пункт 5.1)
            GetStrategy(op.PocketStrategy).MillContour(op, geometry, toolRadius, taperOffset, step, nextZ, contourPoints, center, builder, settings);

            // Возврат в центр и подъем
            builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);

            return true; // Обработка успешно завершена, продолжаем
        }
    }
}
