#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Расстояния изломов замкнутой ломаной от её первой точки — в той же
    /// линейке «длина вдоль контура», которой пользуется
    /// <see cref="IProfileGeometry.GetPointOnContour"/>: обе стороны
    /// накапливают длины одних и тех же сегментов в одном порядке.
    /// </summary>
    internal static class ContourCornerDistances
    {
        /// <summary>
        /// Накопленные расстояния внутренних вершин ломаной. Первая точка
        /// (расстояние ноль) и замыкающая не входят: рампа и так начинается
        /// и заканчивается в точках контура, проходить ей нужно то, что между.
        /// </summary>
        /// <param name="points">Точки замкнутой ломаной в порядке обхода.</param>
        public static IReadOnlyList<double> FromPolyline(IReadOnlyList<(double x, double y)> points)
        {
            if (points == null || points.Count < 3)
                return Array.Empty<double>();

            var distances = new List<double>(points.Count - 2);
            double accumulated = 0.0;
            for (int i = 0; i < points.Count - 2; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                var dx = p2.x - p1.x;
                var dy = p2.y - p1.y;
                accumulated += Math.Sqrt(dx * dx + dy * dy);
                distances.Add(accumulated);
            }

            return distances;
        }
    }
}
