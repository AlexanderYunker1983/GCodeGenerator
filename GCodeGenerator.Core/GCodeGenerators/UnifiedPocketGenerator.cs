using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
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
    /// обработка контура — <see cref="IPocketPocketingStrategy"/> (5 стратегий, фаза 5).
    /// Пункт 5.6 плана: состав и порядок черновых и чистовых проходов
    /// определяет <see cref="PocketPassPlanner"/>, генератор их исполняет.
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
        /// Все значения перечисления зарегистрированы (фаза 5);
        /// неизвестные значения (защита от старых .ygc) обрабатываются спиралью.
        /// </summary>
        private static IPocketPocketingStrategy GetStrategy(PocketStrategy strategy)
        {
            switch (strategy)
            {
                case PocketStrategy.Concentric:
                    return new ConcentricPocketingStrategy();
                case PocketStrategy.Radial:
                    return new RadialPocketingStrategy();
                case PocketStrategy.ZigZag:
                    return new ZigZagPocketingStrategy();
                case PocketStrategy.Lines:
                    return new LinesPocketingStrategy();
                case PocketStrategy.Spiral:
                default:
                    return new SpiralPocketingStrategy();
            }
        }

        /// <summary>
        /// Создаёт геометрию для операции кармана. Все реализации
        /// <see cref="IPocketOperation"/> наследуются от <see cref="OperationBase"/>.
        /// </summary>
        private static IPocketGeometry CreateGeometry(IPocketOperation op)
            => PocketGeometryFactory.Create((OperationBase)op);

        public void Generate(OperationBase operation, ProgramBuilder builder, GCodeSettings settings)
        {
            // Проверяем, что операция является карманом
            if (!(operation is IPocketOperation pocketOp))
                return;

            // Пункт 5.6: черновой и чистовые проходы. Состав и порядок проходов
            // определяет PocketPassPlanner, генератор только исполняет план.
            var plan = PocketPassPlanner.Plan(pocketOp);

            if (plan.SkipComment != null)
                builder.Comment(plan.SkipComment);

            foreach (var pass in plan.Passes)
            {
                if (pass.Kind == PocketPassKind.WallFinishing)
                    MillWallsFinishing(pass.Operation, builder, settings, plan.TaperOriginZ);
                else
                    MillPocket(pass.Operation, CreateGeometry(pass.Operation), builder, settings, plan.TaperOriginZ);
            }
        }

        /// <summary>
        /// Генерирует основную фрезеровку кармана (цикл по слоям + стратегия).
        /// </summary>
        /// <param name="op">Операция кармана.</param>
        /// <param name="geometry">Геометрия контура операции.</param>
        /// <param name="builder">Построитель структурированной программы.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон стенок. Для чистовых
        /// операций (слой припуска) — верх исходного кармана, а не верх слоя.</param>
        private void MillPocket(
            IPocketOperation op,
            IPocketGeometry geometry,
            ProgramBuilder builder,
            GCodeSettings settings,
            double? taperOriginZ = null)
        {
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
                    builder,
                    settings,
                    taperOriginZ),
                builder,
                settings);
        }

        /// <summary>
        /// Генерирует один слой кармана.
        /// </summary>
        /// <param name="op">Операция кармана.</param>
        /// <param name="geometry">Геометрия контура операции.</param>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="step">Шаг обработки.</param>
        /// <param name="currentZ">Z верха слоя.</param>
        /// <param name="nextZ">Рабочая Z слоя.</param>
        /// <param name="builder">Построитель структурированной программы.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон (null — верх операции).</param>
        /// <param name="strategy">Стратегия обработки (null — по <c>op.PocketStrategy</c>).</param>
        /// <returns>true, если обработку нужно продолжить; false, если контур слишком маленький и обработку нужно прекратить</returns>
        private bool GenerateLayer(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double step,
            double currentZ,
            double nextZ,
            ProgramBuilder builder,
            GCodeSettings settings,
            double? taperOriginZ = null,
            IPocketPocketingStrategy strategy = null)
        {
            int decimals = op.Decimals;

            double depthFromTop = (taperOriginZ ?? op.ContourHeight) - nextZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            var activeStrategy = strategy ?? GetStrategy(op.PocketStrategy);

            // Для DXF-операций слой состоит из областей, на которые распадается
            // эквидистанта каждого замкнутого контура (см. DxfPocketLayerGenerator).
            if (op is PocketDxfOperation dxfOp)
            {
                return _dxfLayerGenerator.GenerateLayer(
                    dxfOp, toolRadius, taperOffset, step,
                    currentZ, nextZ, activeStrategy, builder, settings);
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
            activeStrategy.MillContour(op, geometry, toolRadius, taperOffset, step, nextZ, contourPoints, center, builder, settings);

            // Возврат в центр и подъем
            builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);

            return true; // Обработка успешно завершена, продолжаем
        }

        /// <summary>
        /// Чистовая обработка стенок (пункт 5.6 плана): цикл по слоям слоя припуска,
        /// каждый слой — замкнутый контур <c>GetContour(toolRadius, taperOffset)</c>
        /// (режущая кромка фрезы точно на стенке). Уклон измеряется от верха
        /// исходного кармана (<paramref name="taperOriginZ"/>). Для DXF — по
        /// каждому контуру (с подъёмом на SafeZ между контурами).
        /// </summary>
        private void MillWallsFinishing(
            IPocketOperation wallOp,
            ProgramBuilder builder,
            GCodeSettings settings,
            double taperOriginZ)
        {
            double toolRadius = wallOp.ToolDiameter / 2.0;
            double stepPercent = (wallOp.StepPercentOfTool <= 0) ? 40 : wallOp.StepPercentOfTool;
            double step = GCodeGenerationHelper.CalculateStep(wallOp.ToolDiameter, stepPercent);

            var geometry = CreateGeometry(wallOp);

            _helper.GenerateLayerLoop(
                wallOp,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    wallOp,
                    geometry,
                    toolRadius,
                    step,
                    currentZ,
                    nextZ,
                    builder,
                    settings,
                    taperOriginZ: taperOriginZ,
                    strategy: WallFinishingStrategy.Instance),
                builder,
                settings);
        }

        /// <summary>
        /// Стратегия чистовой обработки стенок (пункт 5.6 плана): замкнутый контур
        /// (режущая кромка фрезы на стенке). Используется <see cref="MillWallsFinishing"/>
        /// независимо от выбранной стратегии черновой обработки.
        /// </summary>
        private sealed class WallFinishingStrategy : IPocketPocketingStrategy
        {
            public static readonly WallFinishingStrategy Instance = new WallFinishingStrategy();

            public void MillContour(
                IPocketOperation op,
                IPocketGeometry geometry,
                double toolRadius,
                double taperOffset,
                double step,
                double workingZ,
                List<(double x, double y)> contourPoints,
                (double x, double y) center,
                ProgramBuilder builder,
                GCodeSettings settings)
            {
                // Стратегия работает на рабочей Z без отводов — workingZ не используется.
                int decimals = op.Decimals;

                if (contourPoints == null || contourPoints.Count < 3)
                    return;

                // Фрезеруем замкнутый контур (инструмент на рабочей Z)
                foreach (var point in contourPoints)
                {
                    builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork, decimals: decimals);
                }

                // Замыкаем контур, если первая точка не совпадает с последней
                var first = contourPoints[0];
                var last = contourPoints[contourPoints.Count - 1];
                const double tolerance = GeometryTolerances.Degenerate;
                if (Math.Abs(first.x - last.x) > tolerance || Math.Abs(first.y - last.y) > tolerance)
                {
                    builder.LinearTo(x: first.x, y: first.y, feed: op.FeedXYWork, decimals: decimals);
                }
            }
        }

        /// <summary>
        /// Проверяет, не стал ли карман слишком маленьким (пункт 5.6 плана):
        /// геометрия операции с учётом радиуса инструмента и уклона стенок
        /// (худший случай — на дне, глубина = TotalDepth).
        /// </summary>
        private static bool IsOperationTooSmall<T>(T op) where T : IPocketOperation
        {
            if (op == null)
                return true;

            double toolRadius = op.ToolDiameter / 2.0;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(op.TotalDepth, op.WallTaperAngleDeg);
            return CreateGeometry(op).IsContourTooSmall(toolRadius, taperOffset);
        }
    }
}
