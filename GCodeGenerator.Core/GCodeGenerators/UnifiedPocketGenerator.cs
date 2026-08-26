#nullable enable
using System;
using System.Linq;
using System.Threading;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
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
        private readonly IPocketStrategyRegistry _strategies;

        /// <summary>Генератор со стандартным набором стратегий.</summary>
        public UnifiedPocketGenerator()
            : this(new PocketStrategyRegistry())
        {
        }

        /// <summary>
        /// Генератор с внешним реестром стратегий: способ выборки можно
        /// расширить, не меняя генератор.
        /// </summary>
        /// <param name="strategies">Реестр «способ выборки → стратегия».</param>
        public UnifiedPocketGenerator(IPocketStrategyRegistry strategies)
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _helper = new PocketGenerationHelper();
            _dxfLayerGenerator = new DxfPocketLayerGenerator();
        }

        /// <summary>Создаёт геометрию для операции кармана.</summary>
        private static IPocketGeometry CreateGeometry(PocketOperationBase op)
            => OperationCatalog.CreatePocketGeometry(op);

        public void Generate(
            OperationBase operation,
            ToolPathBuilder builder,
            GCodeSettings settings,
            CancellationToken cancellation = default)
        {
            // Проверяем, что операция является карманом
            if (!(operation is PocketOperationBase pocketOp))
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
                    : _strategies.For(pass.Operation.PocketStrategy);

                MillPocket(pass.Operation, strategy, pass.Allowance, builder, settings, plan.TaperOriginZ, cancellation);
            }
        }

        /// <summary>
        /// Генерирует основную фрезеровку кармана (цикл по слоям + стратегия).
        /// </summary>
        /// <param name="op">Операция кармана.</param>
        /// <param name="strategy">Способ обхода слоя.</param>
        /// <param name="allowance">Припуск у стенки: отступ траектории внутрь.</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон стенок. Для чистовых
        /// операций (слой припуска) — верх исходного кармана, а не верх слоя.</param>
        /// <param name="cancellation">Отмена: проверяется перед каждым слоем.</param>
        private void MillPocket(
            PocketOperationBase op,
            IPocketPocketingStrategy strategy,
            double allowance,
            ToolPathBuilder builder,
            GCodeSettings settings,
            double? taperOriginZ = null,
            CancellationToken cancellation = default)
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
                    allowance,
                    step,
                    currentZ,
                    nextZ,
                    strategy,
                    builder,
                    settings,
                    taperOriginZ),
                builder,
                settings,
                cancellation);
        }

        /// <summary>
        /// Генерирует один слой кармана.
        /// </summary>
        /// <param name="op">Операция кармана.</param>
        /// <param name="geometry">Геометрия контура операции.</param>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="allowance">Припуск у стенки: отступ траектории внутрь.</param>
        /// <param name="step">Шаг обработки.</param>
        /// <param name="currentZ">Z верха слоя.</param>
        /// <param name="nextZ">Рабочая Z слоя.</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <param name="strategy">Способ обхода слоя.</param>
        /// <param name="taperOriginZ">Z, от которой измеряется уклон (null — верх операции).</param>
        /// <returns>true, если обработку нужно продолжить; false, если контур слишком маленький и обработку нужно прекратить</returns>
        private bool GenerateLayer(
            PocketOperationBase op,
            IPocketGeometry geometry,
            double toolRadius,
            double allowance,
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

            // Отступ траектории от стенки: радиус фрезы и припуск, который
            // проход оставляет для чистовой обработки.
            double contourOffset = toolRadius + allowance;

            // Смещение внутрь может разбить карман на отдельные области —
            // тогда каждая фрезеруется как самостоятельный карман
            // (см. DxfPocketLayerGenerator).
            if (geometry.SplitsIntoAreas)
            {
                return _dxfLayerGenerator.GenerateLayer(
                    op, geometry, toolRadius, allowance, taperOffset, step,
                    currentZ, nextZ, strategy, builder, settings);
            }

            // Проверяем, не стал ли контур слишком маленьким для обработки (для не-DXF операций)
            if (geometry.IsContourTooSmall(contourOffset, taperOffset))
            {
                // Контур слишком маленький - прекращаем обработку
                return false;
            }

            // Получаем контур кармана
            var contour = geometry.GetContour(contourOffset, taperOffset);
            if (contour == null)
                return false;

            var contourPoints = contour.GetPoints().ToList();
            if (contourPoints.Count == 0)
                return false;

            // Точка врезания: центр фигуры, а если он вне области — как у
            // вогнутого контура — внутренняя точка по скан-линии. Базовые
            // фигуры выпуклы, и для них проверка ничего не меняет.
            var center = PocketEntryPoint.Choose(
                geometry, contourOffset, taperOffset, contourPoints, geometry.GetCenter(), step);

            // Перемещаемся к точке врезания кармана
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);
            builder.RapidTo(x: center.x, y: center.y, feed: op.FeedXYRapid);
            builder.RapidTo(z: currentZ, feed: op.FeedZRapid);
            builder.LinearTo(z: nextZ, feed: op.FeedZWork);

            // Обработка слоя выбранным способом обхода.
            strategy.MillContour(
                new PocketLayerContext(
                    op, geometry, toolRadius, allowance, taperOffset, step, nextZ, contourPoints, center, settings),
                builder);

            // Возврат в центр и подъем
            builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork);
            builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);

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
                var contourPoints = layer.ContourPoints;

                if (contourPoints == null || contourPoints.Count < 3)
                    return;

                // Фрезеруем замкнутый контур (инструмент на рабочей Z)
                foreach (var point in contourPoints)
                {
                    builder.LinearTo(x: point.x, y: point.y, feed: op.FeedXYWork);
                }

                // Замыкаем контур, если первая точка не совпадает с последней
                GCodeGenerationHelper.CloseContour(
                    builder, contourPoints, op.FeedXYWork, GeometryTolerances.Degenerate);
            }
        }
    }
}
