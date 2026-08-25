using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Представляет геометрию кармана для генерации G-кода.
    /// Абстрагирует работу с различными типами карманов (круг, прямоугольник, эллипс, DXF).
    ///
    /// Припуск черновой/чистовой обработки применяется не к геометрии, а к копии
    /// операции (<see cref="Helpers.PocketGenerationHelper.ProcessRoughingFinishing"/>):
    /// диаметр инструмента увеличивается на удвоенный припуск, что эквивалентно
    /// смещению траектории внутрь и работает одинаково для всех типов карманов.
    /// Поэтому методы применения припуска к геометрии здесь не объявляются.
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
        /// Проверить, достаточно ли большой контур для обработки с учетом радиуса инструмента и уклона стенок.
        /// </summary>
        /// <param name="toolRadius">Радиус инструмента</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок</param>
        /// <returns>true, если контур слишком маленький для обработки (меньше диаметра фрезы)</returns>
        bool IsContourTooSmall(double toolRadius, double taperOffset);
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
        /// Получить площадь контура. Используется выбором наибольшей области
        /// эквидистанты DXF-кармана и превью операций.
        /// </summary>
        /// <returns>Площадь контура</returns>
        double GetArea();
    }
}
