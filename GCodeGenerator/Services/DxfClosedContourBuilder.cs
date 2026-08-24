using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Восстанавливает замкнутые области из DXF-полилиний, соединяя сегменты
    /// и учитывая точки их пересечения.
    /// </summary>
    internal sealed class DxfClosedContourBuilder
    {
        private const double ClosedContourTolerance = 0.001;
        private readonly DxfSegmentConnector _segmentConnector =
            new DxfSegmentConnector(ClosedContourTolerance);
        private readonly DxfSegmentIntersectionSplitter _intersectionSplitter =
            new DxfSegmentIntersectionSplitter(ClosedContourTolerance);

        internal List<DxfPolyline> Build(List<DxfPolyline> allPolylines)
        {
            // Теперь пытаемся соединить отдельные линии и дуги в замкнутые контуры
            var connectedContours = _segmentConnector.Connect(allPolylines);
            
            // Ищем замкнутые области, образованные пересекающимися линиями
            var intersectionContours = FindClosedAreasFromIntersections(allPolylines);
            
            var closedContours = new List<DxfPolyline>();
            AddUniqueClosedContours(closedContours, allPolylines);
            AddUniqueClosedContours(closedContours, connectedContours);
            AddUniqueClosedContours(closedContours, intersectionContours);

            return closedContours;
        }

        private void AddUniqueClosedContours(
            List<DxfPolyline> destination,
            IEnumerable<DxfPolyline> candidates)
        {
            foreach (var contour in candidates)
            {
                if (IsClosedContour(contour)
                    && !destination.Any(existing => AreContoursSimilar(contour, existing)))
                    destination.Add(contour);
            }
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

        private List<DxfPolyline> FindClosedAreasFromIntersections(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            
            if (segments == null || segments.Count == 0)
                return contours;
            
            // Находим все точки пересечения и разбиваем сегменты
            var splitSegments = _intersectionSplitter.Split(segments);
            
            if (splitSegments == null || splitSegments.Count == 0)
                return contours;
            
            // Строим граф соединений на основе точек (вершин), а не сегментов
            var pointGraph = BuildPointGraph(splitSegments);
            
            if (pointGraph == null || pointGraph.Count == 0)
                return contours;
            
            // Ищем все циклы в графе точек
            var cycles = FindCyclesInPointGraph(pointGraph);
            
            // Фильтруем циклы - оставляем только те, которые образуют замкнутые области
            foreach (var cycle in cycles)
            {
                if (cycle != null && cycle.Count >= 3)
                {
                    var contour = BuildContourFromPointCycle(cycle);
                    if (contour != null && IsClosedContour(contour))
                    {
                        // Проверяем, что контур имеет достаточную площадь (не является вырожденным)
                        var area = GetContourArea(contour);
                        if (area > ClosedContourTolerance * ClosedContourTolerance)
                        {
                            contours.Add(contour);
                        }
                    }
                }
            }
            
            return contours;
        }
        
        private double GetContourArea(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 3)
                return 0;
            
            double area = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(area / 2.0);
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
            
            // Ограничиваем глубину поиска, чтобы избежать бесконечных циклов
            const int maxCycleLength = 100;
            
            // Пробуем начать поиск с каждой точки в графе
            foreach (var startPoint in graph.Keys)
            {
                if (graph[startPoint] == null || graph[startPoint].Count < 2)
                    continue; // Пропускаем точки с менее чем 2 соседями (не могут быть частью цикла)
                
                // Начинаем поиск с каждого соседа начальной точки
                foreach (var firstNeighbor in graph[startPoint])
                {
                    var path = new List<DxfPoint> { startPoint };
                    FindCyclesFromPoint(startPoint, firstNeighbor, graph, path, cycles, foundCycles, maxCycleLength);
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

        private bool AreContoursSimilar(DxfPolyline c1, DxfPolyline c2)
        {
            if (c1?.Points == null || c2?.Points == null)
                return false;
            
            if (Math.Abs(c1.Points.Count - c2.Points.Count) > 2)
                return false;
            
            // Проверяем, совпадают ли точки контуров (с учетом возможного сдвига начала)
            for (int offset = 0; offset < c1.Points.Count; offset++)
            {
                int matchCount = 0;
                for (int i = 0; i < c1.Points.Count && i < c2.Points.Count; i++)
                {
                    int idx1 = (i + offset) % c1.Points.Count;
                    int idx2 = i % c2.Points.Count;
                    if (PointsMatch(c1.Points[idx1], c2.Points[idx2]))
                        matchCount++;
                }
                if (matchCount >= Math.Min(c1.Points.Count, c2.Points.Count) - 1)
                    return true;
            }
            
            return false;
        }

        private bool IsClosedContour(DxfPolyline polyline)
        {
            if (polyline?.Points == null || polyline.Points.Count < 3)
                return false;

            var first = polyline.Points[0];
            var last = polyline.Points[polyline.Points.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

    }
}
