using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Builds a normalized endpoint graph and returns unique closed point cycles.
    /// </summary>
    internal sealed class DxfPointCycleFinder
    {
        // Bounds DFS depth for malformed or highly connected graphs.
        private const int MaxCycleLength = 100;
        private readonly double _tolerance;

        internal DxfPointCycleFinder(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<DxfPolyline> FindContours(List<DxfPolyline> segments)
        {
            var graph = BuildPointGraph(segments);
            if (graph.Count == 0)
                return new List<DxfPolyline>();

            var contours = new List<DxfPolyline>();
            foreach (var cycle in FindCyclesInPointGraph(graph))
            {
                if (cycle != null && cycle.Count >= 3)
                    contours.Add(BuildContourFromPointCycle(cycle));
            }

            return contours;
        }

        private Dictionary<DxfPoint, List<DxfPoint>> BuildPointGraph(List<DxfPolyline> segments)
        {
            var graph = new Dictionary<DxfPoint, List<DxfPoint>>();
            
            // Для каждого сегмента добавляем соединения между его концами
            foreach (var seg in segments)
            {
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                var start = seg.Points[0];
                var end = seg.Points[seg.Points.Count - 1];
                
                // Находим или создаем ключи для точек
                DxfPoint startKey = FindOrAddPoint(graph, start);
                DxfPoint endKey = FindOrAddPoint(graph, end);
                
                // Добавляем соединение (двунаправленное)
                if (!graph[startKey].Any(p => PointsMatch(p, endKey)))
                    graph[startKey].Add(endKey);
                if (!graph[endKey].Any(p => PointsMatch(p, startKey)))
                    graph[endKey].Add(startKey);
            }
            
            return graph;
        }

        private DxfPoint FindOrAddPoint(Dictionary<DxfPoint, List<DxfPoint>> graph, DxfPoint point)
        {
            // Ищем существующую точку в графе
            foreach (var key in graph.Keys)
            {
                if (PointsMatch(key, point))
                    return key;
            }
            
            // Если не нашли, добавляем новую точку
            graph[point] = new List<DxfPoint>();
            return point;
        }

        private List<List<DxfPoint>> FindCyclesInPointGraph(Dictionary<DxfPoint, List<DxfPoint>> graph)
        {
            var cycles = new List<List<DxfPoint>>();
            var foundCycles = new HashSet<string>();
            
            // Пробуем начать поиск с каждой точки в графе
            foreach (var startPoint in graph.Keys)
            {
                if (graph[startPoint] == null || graph[startPoint].Count < 2)
                    continue; // Пропускаем точки с менее чем 2 соседями (не могут быть частью цикла)
                
                // Начинаем поиск с каждого соседа начальной точки
                foreach (var firstNeighbor in graph[startPoint])
                {
                    var path = new List<DxfPoint> { startPoint };
                    FindCyclesFromPoint(startPoint, firstNeighbor, graph, path, cycles, foundCycles, MaxCycleLength);
                }
            }
            
            return cycles;
        }

        private void FindCyclesFromPoint(DxfPoint startPoint, DxfPoint currentPoint, 
            Dictionary<DxfPoint, List<DxfPoint>> graph, List<DxfPoint> path, 
            List<List<DxfPoint>> cycles, HashSet<string> foundCycles, int maxLength)
        {
            // Ограничиваем длину пути
            if (path.Count >= maxLength)
                return;
            
            // Если мы вернулись в начальную точку и прошли минимум 3 точки - это цикл
            if (path.Count > 0 && PointsMatch(currentPoint, startPoint) && path.Count >= 3)
            {
                // Найден цикл - проверяем, не дубликат ли это
                var sortedPath = path.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                var cycleKey = string.Join("|", sortedPath.Select(p => $"{p.X:F6},{p.Y:F6}"));
                if (!foundCycles.Contains(cycleKey))
                {
                    foundCycles.Add(cycleKey);
                    cycles.Add(new List<DxfPoint>(path));
                }
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
                            var sortedPath = path.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
                            var cycleKey = string.Join("|", sortedPath.Select(p => $"{p.X:F6},{p.Y:F6}"));
                            if (!foundCycles.Contains(cycleKey))
                            {
                                foundCycles.Add(cycleKey);
                                cycles.Add(new List<DxfPoint>(path));
                            }
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
                            FindCyclesFromPoint(startPoint, neighbor, graph, path, cycles, foundCycles, maxLength);
                        }
                    }
                }
            }
            
            path.RemoveAt(path.Count - 1);
        }

        private DxfPolyline BuildContourFromPointCycle(List<DxfPoint> cycle)
        {
            // Строим контур из цикла точек
            var contourPoints = new List<DxfPoint>();
            foreach (var point in cycle)
            {
                if (contourPoints.Count == 0 || !PointsMatch(contourPoints[contourPoints.Count - 1], point))
                {
                    contourPoints.Add(new DxfPoint { X = point.X, Y = point.Y });
                }
            }
            
            // Замыкаем контур
            if (contourPoints.Count > 0 && !PointsMatch(contourPoints[0], contourPoints[contourPoints.Count - 1]))
            {
                contourPoints.Add(new DxfPoint { X = contourPoints[0].X, Y = contourPoints[0].Y });
            }
            
            return new DxfPolyline { Points = contourPoints };
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
            => Geometry2D.PointsMatch(p1, p2, _tolerance);
    }
}
