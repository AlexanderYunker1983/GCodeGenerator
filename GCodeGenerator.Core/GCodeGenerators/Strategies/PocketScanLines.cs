#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Скан-линии: сечение замкнутого контура прямыми (пункты 5.4/5.5 плана).
    /// Общая геометрия для стратегий <see cref="ZigZagPocketingStrategy"/> и
    /// <see cref="LinesPocketingStrategy"/>: контур поворачивается на
    /// −LineAngleDeg вокруг центра (направление резания — вдоль локальной X),
    /// горизонтальные скан-линии ставятся в серединах равных полос
    /// высотой ≤ step (линии не ложатся на границу контура).
    /// </summary>
    public static class PocketScanLines
    {
        /// <summary>
        /// Одна скан-линия: локальная координата Y и сегменты сечения
        /// (пары X в порядке возрастания слева направо).
        /// </summary>
        public readonly struct ScanLine
        {
            public ScanLine(double y, List<(double x1, double x2)> segments)
            {
                Y = y;
                Segments = segments;
            }

            /// <summary>Локальная координата Y линии (направление резания — вдоль X).</summary>
            public double Y { get; }

            /// <summary>Сегменты сечения: (x1, x2), x1 &lt; x2, по порядку слева направо.</summary>
            public List<(double x1, double x2)> Segments { get; }
        }

        /// <summary>
        /// Строит скан-линии для контура.
        /// </summary>
        /// <param name="contourPoints">Точки контура (траектория центра инструмента).</param>
        /// <param name="center">Центр контура (ось поворота).</param>
        /// <param name="angleDeg">Угол направления резания, градусы к оси X (LineAngleDeg).</param>
        /// <param name="step">Шаг обработки (высота полос ≤ step).</param>
        /// <returns>Скан-линии в локальных координатах, от «нижней» к «верхней».</returns>
        public static List<ScanLine> Build(
            List<(double x, double y)> contourPoints,
            (double x, double y) center,
            double angleDeg,
            double step)
        {
            var result = new List<ScanLine>();
            if (contourPoints == null || contourPoints.Count < 3 || step <= 0)
                return result;

            double angle = -angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            // Локальные координаты: направление резания — вдоль X
            var local = new List<(double x, double y)>(contourPoints.Count);
            double yMin = double.MaxValue;
            double yMax = double.MinValue;
            foreach (var p in contourPoints)
            {
                double dx = p.x - center.x;
                double dy = p.y - center.y;
                double lx = dx * cos - dy * sin;
                double ly = dx * sin + dy * cos;
                local.Add((lx, ly));
                if (ly < yMin) yMin = ly;
                if (ly > yMax) yMax = ly;
            }

            double height = yMax - yMin;
            if (height <= GeometryTolerances.Degenerate)
                return result;

            // Равные полосы высотой ≤ step; линия — в середине полосы
            int bands = Math.Max(1, (int)Math.Ceiling(height / step));
            double band = height / bands;

            for (int k = 0; k < bands; k++)
            {
                double y = yMin + (k + 0.5) * band;
                var segments = IntersectLine(local, y);
                if (segments.Count > 0)
                    result.Add(new ScanLine(y, segments));
            }

            return result;
        }

        /// <summary>
        /// Переводит локальную точку в мировые координаты (обратное к <see cref="Build"/>).
        /// </summary>
        public static (double x, double y) ToWorld(
            (double x, double y) local,
            (double x, double y) center,
            double angleDeg)
        {
            double angle = angleDeg * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return (
                center.x + local.x * cos - local.y * sin,
                center.y + local.x * sin + local.y * cos);
        }

        /// <summary>
        /// Пересечения горизонтальной линии y = const с многоугольником:
        /// X-координаты отсортированы, сгруппированы в пары (сегменты).
        /// </summary>
        private static List<(double x1, double x2)> IntersectLine(
            List<(double x, double y)> local,
            double y)
        {
            const double eps = GeometryTolerances.Degenerate;
            var xs = new List<double>();

            for (int i = 0; i < local.Count; i++)
            {
                var p1 = local[i];
                var p2 = local[(i + 1) % local.Count];

                double dy = p2.y - p1.y;
                if (Math.Abs(dy) < eps)
                    continue; // горизонтальный сегмент скан-линию не пересекает

                double t = (y - p1.y) / dy;
                if (t < -GeometryTolerances.Vertex || t > 1.0 + GeometryTolerances.Vertex)
                    continue;

                double x = p1.x + t * (p2.x - p1.x);
                xs.Add(x);
            }

            xs.Sort();

            // Удаляем близкие дубликаты (вершина, лежащая на линии)
            var unique = new List<double>();
            foreach (var x in xs)
            {
                if (unique.Count == 0 || Math.Abs(x - unique[unique.Count - 1]) > GeometryTolerances.Vertex)
                    unique.Add(x);
            }

            // Паруем: (0,1), (2,3), ... — вход/выход из контура
            var segments = new List<(double x1, double x2)>();
            for (int i = 0; i + 1 < unique.Count; i += 2)
            {
                if (unique[i + 1] - unique[i] > GeometryTolerances.Vertex)
                    segments.Add((unique[i], unique[i + 1]));
            }

            return segments;
        }
    }
}
