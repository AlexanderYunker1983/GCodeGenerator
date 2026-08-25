using System.Collections.Generic;
using Clipper2Lib;
using GCodeGenerator.Models;

namespace GCodeGenerator.Geometry
{
    /// <summary>
    /// Эквидистанта замкнутого контура.
    ///
    /// Прежняя реализация строила параллельные прямые к сторонам и обрезала их
    /// по пересечениям соседей. На выпуклых контурах это работает, но у
    /// вогнутых при смещении внутрь возникают петли самопересечения, которые
    /// такой алгоритм не удаляет: результат выглядел как «бабочка» или как
    /// маленький вывернутый многоугольник с ненулевой площадью. Отсюда взялись
    /// эвристики отсечки слоёв (рост площади, смена направления обхода, разворот
    /// векторов вершин, оценка «песочных часов») — они пытались распознать уже
    /// испорченный результат вместо того, чтобы получать правильный.
    ///
    /// Теперь смещение выполняет Clipper2 — он удаляет петли и, если область
    /// при смещении распадается на несколько частей, возвращает их все. Для
    /// кармана это физически верно: узкая перемычка исчезает раньше остальной
    /// области, и дальше фрезеруются отдельные карманы.
    /// </summary>
    public static class ContourOffset
    {
        /// <summary>
        /// Знаков после запятой во внутреннем представлении Clipper2.
        /// Соответствует <see cref="GeometryTolerances.Vertex"/>: координаты
        /// различаются с той же точностью, с какой генератор считает вершины
        /// совпадающими.
        /// </summary>
        private const int Precision = 6;

        /// <summary>
        /// Ограничение выброса острого угла при стыковке смещённых сторон.
        /// Значение 2 означает, что вершина не может уйти дальше двух величин
        /// смещения — на очень острых углах она срезается. Прежний алгоритм
        /// продлевал стороны до пересечения без ограничения.
        /// </summary>
        private const double MiterLimit = 2.0;

        /// <summary>
        /// Смещает замкнутый контур и возвращает все получившиеся области.
        /// </summary>
        /// <param name="contour">Вершины исходного контура. Замыкающая точка,
        /// совпадающая с первой, необязательна.</param>
        /// <param name="delta">Величина смещения: отрицательная — внутрь области,
        /// положительная — наружу.</param>
        /// <returns>
        /// Список замкнутых контуров без замыкающего дубликата вершины.
        /// Пустой список означает, что при таком смещении области не остаётся:
        /// для кармана это признак «фреза не помещается».
        /// </returns>
        public static List<List<DxfPoint>> Offset(IReadOnlyList<DxfPoint> contour, double delta)
        {
            var result = new List<List<DxfPoint>>();
            var source = ToPath(contour);
            if (source.Count < 3)
                return result;

            // Clipper2 трактует смещение относительно направления обхода:
            // отрицательная дельта уменьшает область только у контура с
            // положительной площадью (обход против часовой стрелки).
            if (!Clipper.IsPositive(source))
                source.Reverse();

            var offset = Clipper.InflatePaths(
                new PathsD { source },
                delta,
                JoinType.Miter,
                EndType.Polygon,
                MiterLimit,
                Precision);

            foreach (var path in offset)
            {
                if (path.Count < 3)
                    continue;

                var points = new List<DxfPoint>(path.Count);
                foreach (var point in path)
                    points.Add(new DxfPoint { X = point.x, Y = point.y });
                result.Add(points);
            }

            return result;
        }

        /// <summary>
        /// Переводит вершины контура в путь Clipper2, отбрасывая замыкающий
        /// дубликат первой точки: для Clipper2 путь замкнут по определению,
        /// а повторная вершина даёт вырожденную сторону нулевой длины.
        /// </summary>
        private static PathD ToPath(IReadOnlyList<DxfPoint> contour)
        {
            var path = new PathD();
            if (contour == null || contour.Count == 0)
                return path;

            int count = contour.Count;
            var first = contour[0];
            var last = contour[count - 1];
            if (count > 1 && Geometry2D.PointsMatch(first, last, GeometryTolerances.Vertex))
                count--;

            for (int i = 0; i < count; i++)
            {
                var point = contour[i];
                if (point == null)
                    continue;
                path.Add(new PointD(point.X, point.Y));
            }

            return path;
        }
    }
}
