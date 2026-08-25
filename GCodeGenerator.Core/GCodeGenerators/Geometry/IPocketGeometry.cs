#nullable enable
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Представляет геометрию кармана для генерации G-кода.
    /// Абстрагирует работу с различными типами карманов (круг, прямоугольник, эллипс, DXF).
    ///
    /// Припуск черновой/чистовой обработки применяется не к геометрии, а к копии
    /// операции (<see cref="PocketPassPlanner"/>):
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

        /// <summary>
        /// Смещение внутрь может разбить карман на несколько отдельных
        /// областей — так ведёт себя карман из чертежа, где узкая перемычка
        /// исчезает раньше остального.
        ///
        /// У фигуры с одним контуром такого не бывает: смещение оставляет
        /// её одной областью, пока она не выродится совсем. Признак объявлен
        /// здесь, а не выясняется проверкой типа операции снаружи: раньше
        /// генератор спрашивал «а не чертёж ли это» и обходил собственную
        /// абстракцию.
        /// </summary>
        bool SplitsIntoAreas { get; }

        /// <summary>
        /// Области, на которые распадается карман после смещения внутрь.
        /// Каждая — самостоятельный карман с уже готовой траекторией
        /// центра фрезы, поэтому смещение к ней применять больше не нужно.
        ///
        /// У геометрии, которая не распадается, областей нет: она
        /// обрабатывается целиком.
        /// </summary>
        /// <param name="toolRadius">Отступ от стенки: радиус фрезы с припуском.</param>
        /// <param name="taperOffset">Смещение из-за уклона стенок.</param>
        IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset);
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
