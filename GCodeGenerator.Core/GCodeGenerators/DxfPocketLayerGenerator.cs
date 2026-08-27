#nullable enable
using System;
using System.Linq;
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
        /// <param name="op">Операция кармана: подачи, высоты, число знаков.</param>
        /// <param name="geometry">Геометрия кармана, распадающегося на области.</param>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="allowance">Припуск у стенки: отступ траектории внутрь.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок на глубине слоя.</param>
        /// <param name="step">Шаг обработки.</param>
        /// <param name="currentZ">Z верха слоя.</param>
        /// <param name="nextZ">Рабочая Z слоя.</param>
        /// <param name="strategy">Стратегия обработки (выбирается по <c>op.PocketStrategy</c>, пункт 5.1).</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        /// <returns>true, если хотя бы одна область была обработана и обработку нужно продолжить; false, если областей не осталось</returns>
        public bool GenerateLayer(
            PocketOperationBase op,
            IPocketGeometry geometry,
            double toolRadius,
            double allowance,
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
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            bool isFirstArea = true;
            bool atLeastOneAreaProcessed = false;

            // Отступ от стенки: радиус фрезы вместе с припуском. Области
            // приходят готовыми — точки в них уже описывают траекторию центра
            // фрезы, поэтому стратегия получает нулевые отступ и уклон.
            foreach (var area in geometry.GetAreas(toolRadius + allowance, taperOffset))
            {
                var contourPoints = area.GetContour(0, 0).GetPoints().ToList();
                if (contourPoints.Count < 3)
                    continue;

                // Точка врезания: центроид, а у вогнутой области, где центроид
                // лежит вне её, — внутренняя точка по скан-линии. Стратегия
                // получает эту же точку центром: спираль и радиальные проходы
                // расходятся из неё, и она обязана лежать в области.
                var center = PocketEntryPoint.Choose(
                    area,
                    0,
                    0,
                    contourPoints,
                    area.GetCenter(),
                    step,
                    op.EntryMode == PocketEntryMode.Helical ? op.HelicalEntryDiameter / 2.0 : 0.0);

                // Для первой области сохраняется исходное положение после
                // подхода операции; перед каждой следующей выполняется
                // отдельный безопасный подъём. Сам вход общий с обычными
                // карманами и умеет вертикальную и винтовую траектории.
                PocketEntryGenerator.Generate(
                    op,
                    area,
                    0,
                    0,
                    contourPoints,
                    center,
                    currentZ,
                    nextZ,
                    moveToSafeZ: !isFirstArea,
                    builder,
                    settings);

                var layer = new PocketLayerContext(
                    op, area, 0, 0, 0, step, nextZ, contourPoints, center, settings);
                strategy.MillContour(layer, builder);

                // При островах прямая связка к центру может пересечь
                // запрещённую область, поэтому сначала выполняется подъём.
                if (!layer.RequiresSafeTransitions)
                    builder.LinearTo(x: center.x, y: center.y, feed: op.FeedXYWork);
                builder.RapidTo(z: op.SafeZHeight, feed: op.FeedZRapid);

                isFirstArea = false;
                atLeastOneAreaProcessed = true;
            }

            return atLeastOneAreaProcessed;
        }
    }
}
