using System;
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Представляет геометрию кармана для генерации G-кода.
    /// Абстрагирует работу с различными типами карманов (круг, прямоугольник, эллипс, DXF).
    /// </summary>
    public interface IPocketGeometry
    {
        /// <summary>
        /// Получить центр кармана.
        /// </summary>
        /// <returns>Координаты центра (x, y)</returns>
        (double x, double y) GetCenter();

        /// <summary>
        /// Получить контур кармана с учетом компенсации инструмента и уклона стенок.
        /// </summary>
        /// <param name="toolRadius">Радиус инструмента</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок</param>
        /// <returns>Контур кармана</returns>
        IContour GetContour(double toolRadius, double taperOffset);

        /// <summary>
        /// Проверить, находится ли точка внутри кармана.
        /// </summary>
        /// <param name="x">X координата точки</param>
        /// <param name="y">Y координата точки</param>
        /// <param name="toolRadius">Радиус инструмента</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок</param>
        /// <returns>true, если точка находится внутри кармана</returns>
        bool IsPointInside(double x, double y, double toolRadius, double taperOffset);

        /// <summary>
        /// Применить припуск для черновой обработки.
        /// Уменьшает размеры кармана на величину припуска.
        /// </summary>
        /// <param name="allowance">Величина припуска</param>
        /// <returns>Новая геометрия с примененным припуском</returns>
        IPocketGeometry ApplyRoughingAllowance(double allowance);

        /// <summary>
        /// Применить припуск для чистовой обработки дна.
        /// Уменьшает размеры кармана на величину припуска для обработки только дна.
        /// </summary>
        /// <param name="allowance">Величина припуска</param>
        /// <returns>Новая геометрия с примененным припуском</returns>
        IPocketGeometry ApplyBottomFinishingAllowance(double allowance);

        /// <summary>
        /// Проверить, не стал ли карман слишком маленьким после применения припуска.
        /// </summary>
        /// <returns>true, если карман слишком маленький для обработки</returns>
        bool IsTooSmall();

        /// <summary>
        /// Получить параметры операции для клонирования.
        /// </summary>
        /// <returns>Параметры операции</returns>
        IPocketOperationParameters GetParameters();
    }

    /// <summary>
    /// Контур кармана - последовательность точек, образующих замкнутый контур.
    /// </summary>
    public interface IContour
    {
        /// <summary>
        /// Получить точки контура.
        /// </summary>
        /// <returns>Последовательность точек (x, y)</returns>
        IEnumerable<(double x, double y)> GetPoints();

        /// <summary>
        /// Получить площадь контура.
        /// </summary>
        /// <returns>Площадь контура</returns>
        double GetArea();

        /// <summary>
        /// Получить периметр контура.
        /// </summary>
        /// <returns>Периметр контура</returns>
        double GetPerimeter();
    }

    /// <summary>
    /// Параметры операции кармана для клонирования и модификации.
    /// </summary>
    public interface IPocketOperationParameters
    {
        /// <summary>
        /// Общая глубина обработки.
        /// </summary>
        double TotalDepth { get; set; }

        /// <summary>
        /// Высота контура (начальная Z координата).
        /// </summary>
        double ContourHeight { get; set; }

        /// <summary>
        /// Включена ли черновая обработка.
        /// </summary>
        bool IsRoughingEnabled { get; set; }

        /// <summary>
        /// Включена ли чистовая обработка.
        /// </summary>
        bool IsFinishingEnabled { get; set; }

        /// <summary>
        /// Припуск на обработку.
        /// </summary>
        double FinishAllowance { get; set; }
    }
}

