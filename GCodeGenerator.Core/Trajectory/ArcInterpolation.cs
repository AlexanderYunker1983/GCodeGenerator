#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// Разбиение дуги на хорды — общая формула всех предпросмотров.
    ///
    /// Формула существовала в трёх дословных копиях: построитель 3D-сцены
    /// из траектории, разбор готовой программы и плоская проекция для
    /// 2D-предпросмотра. Инвариант «сцены обязаны совпадать» держался на
    /// точности копирования — четвёртая копия или правка одной из трёх
    /// разошлись бы молча; теперь он держится на общем коде. На G-код
    /// разбиение не влияет: дуги уходят в программу словами G2/G3, хорды
    /// нужны только экранам.
    ///
    /// Шаг фиксированный — π/16 оборота и не меньше четырёх сегментов, как
    /// было в каждой из копий. Шаг по хордовой ошибке, учитывающий радиус,
    /// изменил бы плотность всех сцен и остаётся отдельным решением.
    /// </summary>
    public static class ArcInterpolation
    {
        /// <summary>Наименьшее число сегментов дуги.</summary>
        public const int MinimumSegments = 4;

        /// <summary>Угловой шаг разбиения, радианы на сегмент.</summary>
        public const double AngleStep = Math.PI / 16.0;

        /// <summary>
        /// Точки дуги в плоскости двух первых координат: пары (a, b) на
        /// окружности вокруг центра плюс доля пути t для интерполяции
        /// третьей оси вызывающим кодом.
        /// </summary>
        /// <param name="startA">Первая координата начала дуги.</param>
        /// <param name="startB">Вторая координата начала дуги.</param>
        /// <param name="endA">Первая координата конца дуги.</param>
        /// <param name="endB">Вторая координата конца дуги.</param>
        /// <param name="centerA">Первая координата центра.</param>
        /// <param name="centerB">Вторая координата центра.</param>
        /// <param name="clockwise">Обход по часовой стрелке.</param>
        /// <param name="includeStart">Выдавать ли начальную точку (t = 0).</param>
        public static IEnumerable<(double a, double b, double t)> Points(
            double startA, double startB,
            double endA, double endB,
            double centerA, double centerB,
            bool clockwise,
            bool includeStart)
        {
            var startAngle = Math.Atan2(startB - centerB, startA - centerA);
            var endAngle = Math.Atan2(endB - centerB, endA - centerA);
            var radius = Math.Sqrt(
                Math.Pow(startA - centerA, 2) + Math.Pow(startB - centerB, 2));

            // Конечный угол нормализуется по направлению обхода: по часовой
            // стрелке угол убывает, против — растёт; полный оборот возможен.
            if (clockwise)
            {
                if (endAngle >= startAngle) endAngle -= 2 * Math.PI;
            }
            else
            {
                if (endAngle <= startAngle) endAngle += 2 * Math.PI;
            }

            var segments = Math.Max((int)(Math.Abs(endAngle - startAngle) / AngleStep), MinimumSegments);

            for (int i = includeStart ? 0 : 1; i <= segments; i++)
            {
                var t = (double)i / segments;
                var angle = startAngle + t * (endAngle - startAngle);
                yield return (
                    centerA + radius * Math.Cos(angle),
                    centerB + radius * Math.Sin(angle),
                    t);
            }
        }
    }
}
