#nullable enable
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
        // Ветвящийся граф по-прежнему разбирается DFS. Ограничение защищает
        // стек процесса, но его достижение является явной ошибкой сложности,
        // а не причиной молча отбросить длинный контур.
        private const int MaxBranchedPathLength = 4096;

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
            var points = new SpatialPointIndex<Point2D>(_tolerance);
            
            // Для каждого сегмента добавляем соединения между его концами
            foreach (var seg in segments)
            {
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                var start = seg.Points[0];
                var end = seg.Points[seg.Points.Count - 1];
                
                // Находим или создаем ключи для точек
                Point2D startKey = FindOrAddPoint(graph, points, start);
                Point2D endKey = FindOrAddPoint(graph, points, end);
                
                // Добавляем соединение (двунаправленное)
                if (!graph[startKey].Any(p => PointsMatch(p, endKey)))
                    graph[startKey].Add(endKey);
                if (!graph[endKey].Any(p => PointsMatch(p, startKey)))
                    graph[endKey].Add(startKey);
            }
            
            return graph;
        }

        private static Point2D FindOrAddPoint(Dictionary<Point2D, List<Point2D>> graph,
            SpatialPointIndex<Point2D> points, Point2D point)
        {
            if (points.TryFindFirst(point, null, out var existing))
                return existing;
            
            // Если не нашли, добавляем новую точку
            graph[point] = new List<Point2D>();
            points.Add(point, point);
            return point;
        }

        private List<List<Point2D>> FindCyclesInPointGraph(
            Dictionary<Point2D, List<Point2D>> graph, CancellationToken cancellation)
        {
            var cycles = new List<List<Point2D>>();
            var foundCycles = new HashSet<string>();
            var steps = 0;

            // Обычный CAD-контур — связная компонента, где у каждой вершины
            // ровно два соседа. Такой контур читается линейно и итеративно:
            // длина не ограничена произвольными 100 вершинами, рекурсивный
            // стек не растёт, а один и тот же цикл не обходится от каждой
            // вершины в обоих направлениях.
            var branchedPoints = new HashSet<Point2D>();
            foreach (var component in FindConnectedComponents(graph, cancellation))
            {
                if (component.All(point => graph[point].Count == 2))
                {
                    AddCycleIfNew(TraceSimpleCycle(component[0], graph, cancellation),
                        cycles, foundCycles);
                }
                else
                {
                    foreach (var point in component)
                        branchedPoints.Add(point);
                }
            }

            // В компонентах с развилками нужен полный перебор циклов.
            foreach (var startPoint in branchedPoints)
            {
                if (graph[startPoint] == null || graph[startPoint].Count < 2)
                    continue; // Пропускаем точки с менее чем 2 соседями (не могут быть частью цикла)

                // Начинаем поиск с каждого соседа начальной точки
                foreach (var firstNeighbor in graph[startPoint])
                {
                    var path = new List<Point2D> { startPoint };
                    var maxLength = Math.Min(branchedPoints.Count + 1, MaxBranchedPathLength);
                    FindCyclesFromPoint(startPoint, firstNeighbor, graph, path, cycles, foundCycles,
                        maxLength, branchedPoints.Count + 1 > MaxBranchedPathLength,
                        ref steps, cancellation);
                }
            }

            return cycles;
        }

        private void FindCyclesFromPoint(Point2D startPoint, Point2D currentPoint,
            Dictionary<Point2D, List<Point2D>> graph, List<Point2D> path,
            List<List<Point2D>> cycles, HashSet<string> foundCycles, int maxLength,
            bool pathLengthWasLimited, ref int steps, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();

            // Бюджет общий на весь чертёж: его превышение означает
            // комбинаторный взрыв, а не большой контур, и честный отказ
            // лучше поиска, который нельзя ни дождаться, ни прервать.
            if (++steps > MaxSearchSteps)
            {
                throw new CoreException(CoreErrorCodes.DxfTooComplex,
                    "The drawing is too complex for closed-contour search: the traversal limit was exceeded. "
                    + "Reduce the number of intersecting lines or close the contour with a polyline.");
            }

            // Ограничиваем длину пути
            if (path.Count >= maxLength)
            {
                if (pathLengthWasLimited)
                {
                    throw new CoreException(CoreErrorCodes.DxfTooComplex,
                        "The drawing is too complex for closed-contour search: the path length limit was exceeded. "
                        + "Reduce the number of intersecting lines or close the contour with a polyline.");
                }

                return;
            }

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
                                maxLength, pathLengthWasLimited, ref steps, cancellation);
                        }
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
        }

        private static List<List<Point2D>> FindConnectedComponents(
            Dictionary<Point2D, List<Point2D>> graph, CancellationToken cancellation)
        {
            var components = new List<List<Point2D>>();
            var remaining = new HashSet<Point2D>(graph.Keys);

            while (remaining.Count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                var start = remaining.First();
                var component = new List<Point2D>();
                var pending = new Stack<Point2D>();
                pending.Push(start);
                remaining.Remove(start);

                while (pending.Count > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var point = pending.Pop();
                    component.Add(point);
                    foreach (var neighbor in graph[point])
                    {
                        if (remaining.Remove(neighbor))
                            pending.Push(neighbor);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static List<Point2D> TraceSimpleCycle(
            Point2D start, Dictionary<Point2D, List<Point2D>> graph,
            CancellationToken cancellation)
        {
            var cycle = new List<Point2D>();
            Point2D? previous = null;
            var current = start;

            do
            {
                cancellation.ThrowIfCancellationRequested();
                cycle.Add(current);
                var neighbors = graph[current];
                var next = previous == null || !ReferenceEquals(neighbors[0], previous)
                    ? neighbors[0]
                    : neighbors[1];
                previous = current;
                current = next;
            }
            while (!ReferenceEquals(current, start));

            return cycle;
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
