#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Представляет геометрию профиля для генерации G-кода.
    /// Абстрагирует работу с различными типами профилей (круг, прямоугольник, эллипс, многоугольник, DXF).
    /// </summary>
    public interface IProfileGeometry
    {
        /// <summary>
        /// Геометрия сама расставляет точки в порядке обхода и может дать
        /// несколько отдельных контуров — так устроен контур из чертежа.
        ///
        /// Обычная фигура даёт один контур, который генератор обходит
        /// по-своему: убирает совпавшие точки, начинает с ближайшей к текущему
        /// положению инструмента и замыкает. Для готовых контуров это не нужно
        /// и вредно — порядок точек в них уже задан смещением.
        ///
        /// Признак объявлен здесь, а не выясняется проверкой типа операции
        /// снаружи: раньше генератор спрашивал «а не чертёж ли это» и обходил
        /// собственную абстракцию, из-за чего добавление любого другого
        /// многоконтурного источника потребовало бы такой же проверки рядом.
        /// </summary>
        bool ProvidesOrderedContours { get; }

        /// <summary>
        /// Готовые контуры в порядке обхода. Пусто, если
        /// <see cref="ProvidesOrderedContours"/> — <c>false</c>.
        /// </summary>
        /// <param name="tolerance">Допуск склейки точек контура.</param>
        IReadOnlyList<IReadOnlyList<(double x, double y)>> GetOrderedContours(double tolerance);

        /// <summary>
        /// Требует ли готовый контур замыкающего рабочего хода. Список точек
        /// намеренно не хранит повтор первой вершины: для открытого профиля
        /// такой повтор был бы ложным резом, поэтому замкнутость передаётся
        /// отдельной семантикой геометрии.
        /// </summary>
        bool IsOrderedContourClosed(IReadOnlyList<(double x, double y)> contour) => false;

        /// <summary>
        /// Получить точки контура с учетом компенсации инструмента.
        /// </summary>
        /// <param name="toolOffset">Смещение траектории инструмента (положительное для Outside, отрицательное для Inside, 0 для OnLine)</param>
        /// <param name="direction">Направление фрезерования</param>
        /// <returns>Последовательность точек контура (x, y)</returns>
        IEnumerable<(double x, double y)> GetContourPoints(
            double toolOffset,
            MillingDirection direction);

        /// <summary>
        /// Получить начальную точку контура.
        /// </summary>
        /// <param name="toolOffset">Смещение траектории инструмента</param>
        /// <returns>Начальная точка контура (x, y)</returns>
        (double x, double y) GetStartPoint(double toolOffset);

        /// <summary>
        /// Получить точку на контуре для рампового входа.
        /// Вычисляет точку на контуре на заданном расстоянии от начальной точки.
        /// </summary>
        /// <param name="distance">Расстояние от начальной точки вдоль контура</param>
        /// <param name="toolOffset">Смещение траектории инструмента</param>
        /// <returns>Точка на контуре (x, y)</returns>
        (double x, double y) GetPointOnContour(double distance, double toolOffset);

        /// <summary>
        /// Получить периметр контура.
        /// </summary>
        /// <param name="toolOffset">Смещение траектории инструмента</param>
        /// <returns>Периметр контура</returns>
        double GetPerimeter(double toolOffset);

        /// <summary>
        /// Расстояния изломов контура от начальной точки вдоль направления
        /// обхода — в той же линейке, что у <see cref="GetPointOnContour"/>.
        /// Рампа входа обязана пройти каждый излом точно: её сэмплы почти
        /// никогда не попадают в вершину, и хорда между соседними сэмплами
        /// срезала бы угол — зарез детали, который не исправить следующим
        /// проходом. Пустой список — контур аналитически гладкий (окружность),
        /// изломов нет.
        /// </summary>
        /// <param name="toolOffset">Смещение траектории инструмента</param>
        IReadOnlyList<double> GetCornerDistances(double toolOffset);

        /// <summary>
        /// Получить сегменты дуг (если есть).
        /// Используется для генерации G2/G3 команд вместо линейной аппроксимации.
        /// </summary>
        /// <param name="toolOffset">Смещение траектории инструмента</param>
        /// <returns>Последовательность сегментов дуг</returns>
        IEnumerable<IArcSegment> GetArcSegments(double toolOffset);

        /// <summary>
        /// Поддержка дуг в G-коде.
        /// </summary>
        bool SupportsArcs { get; }
    }

    /// <summary>
    /// Сегмент дуги для генерации G2/G3 команд.
    /// </summary>
    public interface IArcSegment
    {
        /// <summary>
        /// Начальная точка дуги.
        /// </summary>
        (double x, double y) StartPoint { get; }

        /// <summary>
        /// Конечная точка дуги.
        /// </summary>
        (double x, double y) EndPoint { get; }

        /// <summary>
        /// Центр дуги.
        /// </summary>
        (double x, double y) Center { get; }

        /// <summary>
        /// Радиус дуги.
        /// </summary>
        double Radius { get; }

        /// <summary>
        /// Направление дуги (true для G2/по часовой стрелке, false для G3/против часовой стрелки).
        /// </summary>
        bool IsClockwise { get; }
    }
}
