using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Стратегия фрезерования кармана (пункт 4.6 плана).
    /// Обрабатывает один слой контура кармана: инструмент находится в центре
    /// на рабочей Z, стратегия выводит траекторию обработки; возврат в центр
    /// и подъём на SafeZ выполняет генератор после возврата из стратегии.
    ///
    /// Реализации (фаза 5, D1): <see cref="SpiralPocketingStrategy"/>,
    /// <see cref="ConcentricPocketingStrategy"/>, <see cref="RadialPocketingStrategy"/>,
    /// <see cref="ZigZagPocketingStrategy"/>, <see cref="LinesPocketingStrategy"/>.
    /// Стратегии не хранят состояния между вызовами и существуют в одном
    /// экземпляре — их выдаёт <see cref="PocketStrategies"/>.
    /// </summary>
    public interface IPocketPocketingStrategy
    {
        /// <summary>
        /// Фрезерует один слой контура кармана.
        /// </summary>
        /// <param name="layer">Слой: операция, геометрия, контур, шаг и высота.</param>
        /// <param name="builder">Построитель траектории.</param>
        void MillContour(PocketLayerContext layer, ToolPathBuilder builder);
    }
}
