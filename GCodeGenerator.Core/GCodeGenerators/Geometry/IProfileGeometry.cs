using System;
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

