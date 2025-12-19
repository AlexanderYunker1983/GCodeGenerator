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
        /// Форматирует число с заданным количеством знаков после запятой.
        /// </summary>
        /// <param name="value">Значение для форматирования</param>
        /// <param name="decimals">Количество знаков после запятой</param>
        /// <returns>Отформатированная строка</returns>
        public static string FormatNumber(double value, int decimals)
        {
            var fmt = $"0.{new string('0', decimals)}";
            return value.ToString(fmt, CultureInfo.InvariantCulture);
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
        /// <param name="toolDiameter">Диаметр инструмента</param>
        /// <param name="stepPercentOfTool">Процент от диаметра инструмента (например, 40 означает 40%)</param>
        /// <returns>Шаг обработки</returns>
        public static double CalculateStep(double toolDiameter, double stepPercentOfTool)
        {
            double stepPercent = (stepPercentOfTool <= 0) ? 40 : stepPercentOfTool;
            double step = toolDiameter * (stepPercent / 100.0);
            if (step < 1e-6) step = toolDiameter * 0.4;
            return step;
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

