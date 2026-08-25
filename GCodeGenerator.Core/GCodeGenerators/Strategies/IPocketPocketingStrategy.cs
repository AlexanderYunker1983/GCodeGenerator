using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

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
    /// </summary>
    public interface IPocketPocketingStrategy
    {
        /// <summary>
        /// Фрезерует один слой контура кармана.
        /// </summary>
        /// <param name="op">Операция кармана (подача, направление, Decimals).</param>
        /// <param name="geometry">Геометрия контура (IsPointInside для контроля выхода).</param>
        /// <param name="toolRadius">Радиус инструмента.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок.</param>
        /// <param name="step">Шаг обработки (радиальный шаг спирали).</param>
        /// <param name="workingZ">Рабочая Z слоя (nextZ). Пункт 5.1: нужен стратегиям
        /// с отводами (Lines) — инструмент входит в слой на этой высоте.</param>
        /// <param name="contourPoints">Точки смещённого контура слоя (траектория центра инструмента).</param>
        /// <param name="center">Центр контура (стартовая позиция инструмента, на рабочей Z).</param>
        /// <param name="builder">Построитель траектории.</param>
        /// <param name="settings">Настройки генерации G-кода.</param>
        void MillContour(
            IPocketOperation op,
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset,
            double step,
            double workingZ,
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            ToolPathBuilder builder,
            GCodeSettings settings);
    }
}
