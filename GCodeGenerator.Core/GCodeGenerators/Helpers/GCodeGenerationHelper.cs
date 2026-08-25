#nullable enable
using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Helpers
{
    /// <summary>
    /// Базовый класс-помощник для генерации G-кода.
    /// Содержит общие утилиты для форматирования и расчетов.
    /// </summary>
    public static class GCodeGenerationHelper
    {
        /// <summary>
        /// Форматирует число по шаблону вида "0.000" (InvariantCulture).
        /// Пункт 1.5: математическая библиотека .NET 10 в местах, где .NET Framework
        /// давал 0.0, может давать -0.0 или крошечный остаток (±1e-15, например
        /// cos(3π/2)) — оба форматируются как "-0.000". Скругляем до числа знаков
        /// из fmt и нормализуем -0.0 → 0.0, восстанавливая зафиксированный вывод
        /// (golden-файлы). Для всех остальных значений результат идентичен
        /// прежнему инлайн-форматированию value.ToString(fmt, InvariantCulture).
        /// </summary>
        /// <param name="value">Значение для форматирования</param>
        /// <param name="fmt">Шаблон формата, например "0.000"</param>
        /// <returns>Отформатированная строка</returns>
        public static string FormatNumber(double value, string fmt)
        {
            int decimals = fmt.Length - 2; // "0." + N нулей
            double rounded = Math.Round(value, decimals);
            if (rounded == 0)
                rounded = 0; // нормализация -0.0
            return rounded.ToString(fmt, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Вычисляет радиус инструмента из диаметра.
        /// </summary>
        /// <param name="toolDiameter">Диаметр инструмента</param>
        /// <returns>Радиус инструмента</returns>
        public static double CalculateToolRadius(double toolDiameter)
        {
            return toolDiameter / 2.0;
        }

        /// <summary>
        /// Вычисляет шаг обработки на основе процента от диаметра инструмента.
        /// </summary>
        /// <param name="toolDiameter">Диаметр инструмента (должен быть &gt; 0)</param>
        /// <param name="stepPercentOfTool">Процент от диаметра инструмента (например, 40 означает 40%)</param>
        /// <returns>Шаг обработки</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Диаметр инструмента не больше нуля: шаг получился бы нулевым,
        /// что привело бы к бесконечному циклу спирали (пункт 3.8 плана).
        /// </exception>
        public static double CalculateStep(double toolDiameter, double stepPercentOfTool)
        {
            if (toolDiameter <= 0)
                throw new ArgumentOutOfRangeException(nameof(toolDiameter), toolDiameter,
                    "Tool diameter must be greater than zero (a zero step would make the spiral loop run forever).");
            // Неположительный процент — отказ, а не «разумное» значение
            // вместо заданного: шаг определяет всю траекторию выборки.
            if (!(stepPercentOfTool > 0))
                throw new ArgumentOutOfRangeException(nameof(stepPercentOfTool), stepPercentOfTool,
                    "Step percent of tool diameter must be greater than zero.");

            return toolDiameter * (stepPercentOfTool / 100.0);
        }

        /// <summary>
        /// Вычисляет смещение из-за уклона стенок.
        /// </summary>
        /// <param name="depthFromTop">Глубина от верха (расстояние от начальной высоты до текущей глубины)</param>
        /// <param name="taperAngleDeg">Угол уклона стенок в градусах</param>
        /// <returns>Смещение радиуса из-за уклона</returns>
        public static double CalculateTaperOffset(double depthFromTop, double taperAngleDeg)
        {
            var taperAngleRad = taperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);
            return depthFromTop * taperTan;
        }

        /// <summary>
        /// Вычисляет компенсацию радиуса инструмента для профилей.
        /// </summary>
        /// <param name="mode">Режим траектории инструмента</param>
        /// <param name="toolDiameter">Диаметр инструмента</param>
        /// <returns>Смещение траектории (положительное для Outside, отрицательное для Inside, 0 для OnLine)</returns>
        public static double CalculateToolOffset(ToolPathMode mode, double toolDiameter)
        {
            var toolRadius = CalculateToolRadius(toolDiameter);
            switch (mode)
            {
                case ToolPathMode.Outside:
                    return toolRadius;
                case ToolPathMode.Inside:
                    return -toolRadius;
                case ToolPathMode.OnLine:
                default:
                    return 0.0;
            }
        }
    }
}

