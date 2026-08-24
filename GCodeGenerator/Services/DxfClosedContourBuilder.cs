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

        internal List<DxfPolyline> Build(List<DxfPolyline> allPolylines)
        {
            // Теперь пытаемся соединить отдельные линии и дуги в замкнутые контуры
            var connectedContours = ConnectSegmentsIntoContours(allPolylines);
            
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

        private List<DxfPolyline> ConnectSegmentsIntoContours(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            var used = new bool[segments.Count];
            
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                    continue;
                
                // Пытаемся построить контур, начиная с этого сегмента
                var contour = BuildContourFromSegment(segments, i, used);
                if (contour != null && contour.Points != null && contour.Points.Count >= 3)
                {
                    contours.Add(contour);
                }
            }
            
            return contours;
        }

        private DxfPolyline BuildContourFromSegment(List<DxfPolyline> segments, int startIdx, bool[] used)
        {
            var contourPoints = new List<DxfPoint>();
            var currentSegmentIdx = startIdx;
            var startPoint = segments[startIdx].Points[0];
            var currentPoint = segments[startIdx].Points[segments[startIdx].Points.Count - 1];
            
            // Добавляем точки первого сегмента
            foreach (var p in segments[startIdx].Points)
            {
                contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
            }
            used[startIdx] = true;
            
            // Ищем следующий сегмент, который начинается там, где заканчивается текущий
            while (true)
            {
                int nextSegmentIdx = -1;
                bool reverseNext = false;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                        continue;
                    
                    var seg = segments[i];
                    var segStart = seg.Points[0];
                    var segEnd = seg.Points[seg.Points.Count - 1];
                    
                    // Проверяем, совпадает ли начало или конец сегмента с текущей точкой
                    if (PointsMatch(currentPoint, segStart))
                    {
                        nextSegmentIdx = i;
                        reverseNext = false;
                        break;
                    }
                    else if (PointsMatch(currentPoint, segEnd))
                    {
                        nextSegmentIdx = i;
                        reverseNext = true;
                        break;
                    }
                }
                
                if (nextSegmentIdx < 0)
                    break; // Не нашли следующий сегмент
                
                // Добавляем точки следующего сегмента
                var nextSeg = segments[nextSegmentIdx];
                if (reverseNext)
                {
                    // Добавляем точки в обратном порядке
                    for (int j = nextSeg.Points.Count - 2; j >= 0; j--) // Пропускаем последнюю точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[0];
                }
                else
                {
                    // Добавляем точки в прямом порядке
                    for (int j = 1; j < nextSeg.Points.Count; j++) // Пропускаем первую точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[nextSeg.Points.Count - 1];
                }
                
                used[nextSegmentIdx] = true;
                currentSegmentIdx = nextSegmentIdx;
                
                // Проверяем, замкнулся ли контур
                if (PointsMatch(currentPoint, startPoint))
                {
                    break; // Контур замкнут
                }
            }
            
            return new DxfPolyline { Points = contourPoints };
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
            var splitSegments = SplitSegmentsAtIntersections(segments);
            
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

        private List<DxfPolyline> SplitSegmentsAtIntersections(List<DxfPolyline> segments)
        {
            var splitSegments = new List<DxfPolyline>();
            var intersectionPoints = new Dictionary<int, List<(DxfPoint point, double distance)>>(); // Индекс сегмента -> список точек пересечения с расстоянием от начала
            
            // Находим все пересечения и добавляем точки пересечения к обоим сегментам
            for (int i = 0; i < segments.Count; i++)
            {
                var seg1 = segments[i];
                if (seg1.Points == null || seg1.Points.Count < 2)
                    continue;
                
                if (!intersectionPoints.ContainsKey(i))
                    intersectionPoints[i] = new List<(DxfPoint point, double distance)>();
                
                for (int j = i + 1; j < segments.Count; j++)
                {
                    var seg2 = segments[j];
                    if (seg2.Points == null || seg2.Points.Count < 2)
                        continue;
                    
                    // Находим пересечения между сегментами
                    var pts = FindSegmentIntersections(seg1, seg2);
                    foreach (var pt in pts)
                    {
                        // Вычисляем расстояние от начала сегмента 1 до точки пересечения
                        double dist1 = 0;
                        for (int k = 0; k < seg1.Points.Count - 1; k++)
                        {
                            var p1 = seg1.Points[k];
                            var p2 = seg1.Points[k + 1];
                            var segDist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                            var distToInter = DistanceToSegment(pt.X, pt.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (distToInter < ClosedContourTolerance)
                            {
                                dist1 += Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                                break;
                            }
                            dist1 += segDist;
                        }
                        
                        // Вычисляем расстояние от начала сегмента 2 до точки пересечения
                        double dist2 = 0;
                        for (int k = 0; k < seg2.Points.Count - 1; k++)
                        {
                            var p1 = seg2.Points[k];
                            var p2 = seg2.Points[k + 1];
                            var segDist = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
                            var distToInter = DistanceToSegment(pt.X, pt.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (distToInter < ClosedContourTolerance)
                            {
                                dist2 += Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                                break;
                            }
                            dist2 += segDist;
                        }
                        
                        // Добавляем точку пересечения к обоим сегментам
                        if (!intersectionPoints[i].Any(p => PointsMatch(p.point, pt)))
                            intersectionPoints[i].Add((pt, dist1));
                        
                        if (!intersectionPoints.ContainsKey(j))
                            intersectionPoints[j] = new List<(DxfPoint point, double distance)>();
                        if (!intersectionPoints[j].Any(p => PointsMatch(p.point, pt)))
                            intersectionPoints[j].Add((pt, dist2));
                    }
                }
            }
            
            // Сортируем точки пересечения по расстоянию для каждого сегмента
            foreach (var kvp in intersectionPoints)
            {
                kvp.Value.Sort((a, b) => a.distance.CompareTo(b.distance));
            }
            
            // Разбиваем сегменты в точках пересечения
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;
                
                if (intersectionPoints.ContainsKey(i))
                {
                    var points = new List<DxfPoint>(seg.Points);
                    var intersections = intersectionPoints[i];
                    
                    // Добавляем точки пересечения в правильном порядке (уже отсортированы по расстоянию)
                    foreach (var inter in intersections)
                    {
                        // Находим позицию для вставки точки пересечения
                        int insertPos = -1;
                        double minDist = double.MaxValue;
                        
                        for (int j = 0; j < points.Count - 1; j++)
                        {
                            var p1 = points[j];
                            var p2 = points[j + 1];
                            var dist = DistanceToSegment(inter.point.X, inter.point.Y, p1.X, p1.Y, p2.X, p2.Y);
                            if (dist < minDist && dist < ClosedContourTolerance * 10) // Увеличиваем допуск для поиска
                            {
                                // Проверяем, что точка действительно на отрезке между p1 и p2
                                var dx = p2.X - p1.X;
                                var dy = p2.Y - p1.Y;
                                var segLen = Math.Sqrt(dx * dx + dy * dy);
                                if (segLen > 1e-9)
                                {
                                    var t = ((inter.point.X - p1.X) * dx + (inter.point.Y - p1.Y) * dy) / (segLen * segLen);
                                    if (t >= -0.01 && t <= 1.01) // Небольшой допуск для границ
                                    {
                                        minDist = dist;
                                        insertPos = j + 1;
                                    }
                                }
                            }
                        }
                        
                        if (insertPos >= 0)
                        {
                            // Проверяем, нет ли уже такой точки рядом
                            bool pointExists = false;
                            for (int j = Math.Max(0, insertPos - 1); j < Math.Min(points.Count, insertPos + 2); j++)
                            {
                                if (PointsMatch(points[j], inter.point))
                                {
                                    pointExists = true;
                                    break;
                                }
                            }
                            
                            if (!pointExists)
                            {
                                points.Insert(insertPos, inter.point);
                            }
                        }
                    }
                    
                    // Разбиваем на подсегменты
                    for (int j = 0; j < points.Count - 1; j++)
                    {
                        splitSegments.Add(new DxfPolyline
                        {
                            Points = new List<DxfPoint> { points[j], points[j + 1] }
                        });
                    }
                }
                else
                {
                    // Сегмент без пересечений - добавляем как есть
                    splitSegments.Add(seg);
                }
            }
            
            return splitSegments;
        }

        private List<DxfPoint> FindSegmentIntersections(DxfPolyline seg1, DxfPolyline seg2)
        {
            var intersections = new List<DxfPoint>();
            
            if (seg1.Points == null || seg1.Points.Count < 2 || seg2.Points == null || seg2.Points.Count < 2)
                return intersections;
            
            // Проверяем пересечения между всеми парами отрезков
            for (int i = 0; i < seg1.Points.Count - 1; i++)
            {
                var p1 = seg1.Points[i];
                var p2 = seg1.Points[i + 1];
                
                for (int j = 0; j < seg2.Points.Count - 1; j++)
                {
                    var p3 = seg2.Points[j];
                    var p4 = seg2.Points[j + 1];
                    
                    var intersection = FindLineSegmentIntersection(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
                    if (intersection != null)
                    {
                        if (!intersections.Any(p => PointsMatch(p, intersection)))
                            intersections.Add(intersection);
                    }
                }
            }
            
            return intersections;
        }

        private DxfPoint FindLineSegmentIntersection(double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x4 - x3;
            double dy2 = y4 - y3;
            
            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9)
                return null; // Параллельные линии
            
            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;
            
            // Используем небольшой допуск для границ отрезков
            const double tolerance = 1e-6;
            if (t1 >= -tolerance && t1 <= 1.0 + tolerance && t2 >= -tolerance && t2 <= 1.0 + tolerance)
            {
                // Ограничиваем параметры диапазоном [0, 1]
                t1 = Math.Max(0, Math.Min(1, t1));
                return new DxfPoint
                {
                    X = x1 + t1 * dx1,
                    Y = y1 + t1 * dy1
                };
            }
            
            return null;
        }

        private double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return Math.Sqrt(Math.Pow(px - x1, 2) + Math.Pow(py - y1, 2));
            
            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            double projX = x1 + t * dx;
            double projY = y1 + t * dy;
            return Math.Sqrt(Math.Pow(px - projX, 2) + Math.Pow(py - projY, 2));
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
