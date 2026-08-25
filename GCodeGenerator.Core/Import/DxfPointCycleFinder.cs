using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Builds a normalized endpoint graph and returns unique closed point cycles.
    /// </summary>
    internal sealed class DxfPointCycleFinder
    {
        // Bounds DFS depth for malformed or highly connected graphs.
        private const int MaxCycleLength = 100;

        // Предохранитель от комбинаторного взрыва. Поиск перебирает простые
        // пути, и решётка пересекающихся линий — штриховка, сетка — даёт их
        // экспоненциально много: предел глубины ограничивает длину пути,
        // но не ветвление, и без общего предела шагов импорт такого чертежа
        // не завершается никогда. Контуры реальных чертежей укладываются
        // в тысячи шагов — предел взят с запасом на порядки.
        private const int MaxSearchSteps = 1_000_000;

        private readonly double _tolerance;

        internal DxfPointCycleFinder(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<Polyline2D> FindContours(List<Polyline2D> segments, CancellationToken cancellation = default)
        {
            var graph = BuildPointGraph(segments);
            if (graph.Count == 0)
                return new List<Polyline2D>();

            var contours = new List<Polyline2D>();
            foreach (var cycle in FindCyclesInPointGraph(graph, cancellation))
            {
                if (cycle != null && cycle.Count >= 3)
                    contours.Add(BuildContourFromPointCycle(cycle));
            }

            return contours;
        }

        private Dictionary<Point2D, List<Point2D>> BuildPointGraph(List<Polyline2D> segments)
        {
            var graph = new Dictionary<Point2D, List<Point2D>>();
            
            // Для каждого сегмента добавляем соединения между его концами
            foreach (var seg in segments)
            {
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                var start = seg.Points[0];
                var end = seg.Points[seg.Points.Count - 1];
                
                // Находим или создаем ключи для точек
                Point2D startKey = FindOrAddPoint(graph, start);
                Point2D endKey = FindOrAddPoint(graph, end);
                
                // Добавляем соединение (двунаправленное)
                if (!graph[startKey].Any(p => PointsMatch(p, endKey)))
                    graph[startKey].Add(endKey);
                if (!graph[endKey].Any(p => PointsMatch(p, startKey)))
                    graph[endKey].Add(startKey);
            }
            
            return graph;
        }

        private Point2D FindOrAddPoint(Dictionary<Point2D, List<Point2D>> graph, Point2D point)
        {
            // Ищем существующую точку в графе
            foreach (var key in graph.Keys)
            {
                if (PointsMatch(key, point))
                    return key;
            }
            
            // Если не нашли, добавляем новую точку
            graph[point] = new List<Point2D>();
            return point;
        }

        private List<List<Point2D>> FindCyclesInPointGraph(
            Dictionary<Point2D, List<Point2D>> graph, CancellationToken cancellation)
        {
            var cycles = new List<List<Point2D>>();
            var foundCycles = new HashSet<string>();
            var steps = 0;

            // Пробуем начать поиск с каждой точки в графе
            foreach (var startPoint in graph.Keys)
            {
                if (graph[startPoint] == null || graph[startPoint].Count < 2)
                    continue; // Пропускаем точки с менее чем 2 соседями (не могут быть частью цикла)

                // Начинаем поиск с каждого соседа начальной точки
                foreach (var firstNeighbor in graph[startPoint])
                {
                    var path = new List<Point2D> { startPoint };
                    FindCyclesFromPoint(startPoint, firstNeighbor, graph, path, cycles, foundCycles,
                        MaxCycleLength, ref steps, cancellation);
                }
            }

            return cycles;
        }

        private void FindCyclesFromPoint(Point2D startPoint, Point2D currentPoint,
            Dictionary<Point2D, List<Point2D>> graph, List<Point2D> path,
            List<List<Point2D>> cycles, HashSet<string> foundCycles, int maxLength,
            ref int steps, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            // Бюджет общий на весь чертёж: его превышение означает
            // комбинаторный взрыв, а не большой контур, и честный отказ
            // лучше поиска, который нельзя ни дождаться, ни прервать.
            if (++steps > MaxSearchSteps)
            {
                throw new InvalidOperationException(
                    "Чертёж слишком сложен для поиска замкнутых контуров: превышен предел перебора. "
                    + "Уменьшите число пересекающихся линий или замкните контур полилинией.");
            }

            // Ограничиваем длину пути
            if (path.Count >= maxLength)
                return;

            // Если мы вернулись в начальную точку и прошли минимум 3 точки - это цикл
            if (path.Count > 0 && PointsMatch(currentPoint, startPoint) && path.Count >= 3)
            {
                AddCycleIfNew(path, cycles, foundCycles);
                return;
            }

            // Проверяем, не были ли мы уже в этой точке (кроме начальной)
            for (int i = 0; i < path.Count - 1; i++)
            {
                if (PointsMatch(path[i], currentPoint))
                {
                    return; // Уже были в этой точке
                }
            }

            path.Add(currentPoint);

            if (graph.ContainsKey(currentPoint))
            {
                foreach (var neighbor in graph[currentPoint])
                {
                    // Если сосед - это начальная точка и мы прошли минимум 2 точки - замыкаем цикл
                    if (PointsMatch(neighbor, startPoint))
                    {
                        if (path.Count >= 3)
                        {
                            AddCycleIfNew(path, cycles, foundCycles);
                        }
                    }
                    else
                    {
                        // Проверяем, не были ли мы уже в этой соседней точке
                        bool alreadyVisited = false;
                        for (int i = 0; i < path.Count; i++)
                        {
                            if (PointsMatch(path[i], neighbor))
                            {
                                alreadyVisited = true;
                                break;
                            }
                        }

                        if (!alreadyVisited)
                        {
                            FindCyclesFromPoint(startPoint, neighbor, graph, path, cycles, foundCycles,
                                maxLength, ref steps, cancellation);
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private static void AddCycleIfNew(
            List<Point2D> path, List<List<Point2D>> cycles, HashSet<string> foundCycles)
        {
            if (foundCycles.Add(CycleKey(path)))
                cycles.Add(new List<Point2D>(path));
        }

        /// <summary>
        /// Каноничный ключ цикла — по множеству рёбер. Множества вершин мало:
        /// два разных цикла могут проходить через одни и те же точки разными
        /// рёбрами — квадрат и обход тех же четырёх вершин через диагональ, —
        /// и вершинный ключ склеивал их в один, молча теряя вторую область.
        /// Числа форматируются инвариантно: ключ — внутреннее представление,
        /// и локаль машины не должна на него влиять (в ru-RU запятая дроби
        /// совпадала с разделителем координат ключа).
        /// </summary>
        private static string CycleKey(List<Point2D> path)
        {
            var edges = new List<string>(path.Count);
            for (int i = 0; i < path.Count; i++)
            {
                var a = VertexKey(path[i]);
                var b = VertexKey(path[(i + 1) % path.Count]);
                edges.Add(string.CompareOrdinal(a, b) <= 0 ? a + "-" + b : b + "-" + a);
            }

            edges.Sort(StringComparer.Ordinal);
            return string.Join("|", edges);
        }

        private static string VertexKey(Point2D point)
            => point.X.ToString("F6", CultureInfo.InvariantCulture) + ","
             + point.Y.ToString("F6", CultureInfo.InvariantCulture);

        private Polyline2D BuildContourFromPointCycle(List<Point2D> cycle)
        {
            // Строим контур из цикла точек
            var contourPoints = new List<Point2D>();
            foreach (var point in cycle)
            {
                if (contourPoints.Count == 0 || !PointsMatch(contourPoints[contourPoints.Count - 1], point))
                {
                    contourPoints.Add(new Point2D { X = point.X, Y = point.Y });
                }
            }
            
            // Замыкаем контур
            if (contourPoints.Count > 0 && !PointsMatch(contourPoints[0], contourPoints[contourPoints.Count - 1]))
            {
                contourPoints.Add(new Point2D { X = contourPoints[0].X, Y = contourPoints[0].Y });
            }
            
            return new Polyline2D { Points = contourPoints };
        }

        private bool PointsMatch(Point2D p1, Point2D p2)
            => Geometry2D.PointsMatch(p1, p2, _tolerance);
    }
}
