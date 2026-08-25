using System;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;

using GCodeGenerator.Operations;
using GCodeGenerator.Toolpath;

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
        /// Создаёт геометрию для операции кармана. Все реализации
        /// <see cref="IPocketOperation"/> наследуются от <see cref="OperationBase"/>.
        /// </summary>
        private static IPocketGeometry CreateGeometry(IPocketOperation op)
            => OperationCatalog.CreatePocketGeometry((OperationBase)op);

        public void Generate(OperationBase operation, ToolPathBuilder builder, GCodeSettings settings)
        {
            // Проверяем, что операция является карманом
            if (!(operation is IPocketOperation pocketOp))
                return;

            // Пункт 5.6: черновой и чистовые проходы. Состав и порядок проходов
            // определяет PocketPassPlanner, генератор только исполняет план.
            var plan = PocketPassPlanner.Plan(pocketOp);

            if (plan.SkipComment != null)
                builder.Comment(plan.SkipComment);

            // Проходы плана отличаются только способом обхода слоя: чистовая
            // обработка стенки идёт по замкнутому контуру, остальные — тем
            // способом, который выбран в операции. Цикл по слоям для них общий:
            // прежде он существовал дважды, отдельно для стенки и для дна.
            foreach (var pass in plan.Passes)
            {
                var strategy = pass.Kind == PocketPassKind.WallFinishing
                    ? WallFinishingStrategy.Instance
                    : PocketStrategies.For(pass.Operation.PocketStrategy);

                MillPocket(pass.Operation, strategy, builder, settings, plan.TaperOriginZ);
            }
        }

        /// <summary>
        /// Генерирует основную фрезеровку кармана (цикл по слоям + стратегия).
        /// </summary>
        /// <param name="op">Операция кармана.</param>
        /// <param name="strategy">Способ обхода слоя.</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон стенок. Для чистовых
        /// операций (слой припуска) — верх исходного кармана, а не верх слоя.</param>
        private void MillPocket(
            IPocketOperation op,
            IPocketPocketingStrategy strategy,
            ToolPathBuilder builder,
            GCodeSettings settings,
            double? taperOriginZ = null)
        {
            var geometry = CreateGeometry(op);
            double toolRadius = op.ToolDiameter / 2.0;
            // Шаг проверен предполётным разбором: подставлять «разумное»
            // значение вместо заданного — значит выдать не ту траекторию.
            double step = GCodeGenerationHelper.CalculateStep(op.ToolDiameter, op.StepPercentOfTool);

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
                    strategy,
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
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="strategy">Способ обхода слоя.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон (null — верх операции).</param>
        /// <returns>true, если обработку нужно продолжить; false, если контур слишком маленький и обработку нужно прекратить</returns>
        private bool GenerateLayer(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double step,
            double currentZ,
            double nextZ,
            IPocketPocketingStrategy strategy,
            ToolPathBuilder builder,
            GCodeSettings settings,
            double? taperOriginZ = null)
        {
            int decimals = op.Decimals;

            double depthFromTop = (taperOriginZ ?? op.ContourHeight) - nextZ;
            double taperOffset = GCodeGenerationHelper.CalculateTaperOffset(depthFromTop, op.WallTaperAngleDeg);

            // Для DXF-операций слой состоит из областей, на которые распадается
            // эквидистанта каждого замкнутого контура (см. DxfPocketLayerGenerator).
            if (op is PocketDxfOperation dxfOp)
            {
                return _dxfLayerGenerator.GenerateLayer(
                    dxfOp, toolRadius, taperOffset, step,
                    currentZ, nextZ, strategy, builder, settings);
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

            // Обработка слоя выбранным способом обхода.
            strategy.MillContour(
                new PocketLayerContext(
                    op, geometry, toolRadius, taperOffset, step, nextZ, contourPoints, center, settings),
                builder);

            // Возврат в центр и подъем
            builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork, decimals: decimals);
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid, decimals: decimals);

            return true; // Обработка успешно завершена, продолжаем
        }

        /// <summary>
        /// Стратегия чистовой обработки стенок (пункт 5.6 плана): замкнутый контур
        /// с режущей кромкой фрезы точно на стенке. Выбирается для прохода
        /// <see cref="PocketPassKind.WallFinishing"/> независимо от того, каким
        /// способом выбиралось дно.
        /// </summary>
        private sealed class WallFinishingStrategy : IPocketPocketingStrategy
        {
            public static readonly WallFinishingStrategy Instance = new WallFinishingStrategy();

            public void MillContour(PocketLayerContext layer, ToolPathBuilder builder)
            {
                // Стратегия работает на рабочей Z без отводов — WorkingZ не используется.
                var op = layer.Operation;
                int decimals = op.Decimals;
                var contourPoints = layer.ContourPoints;

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
