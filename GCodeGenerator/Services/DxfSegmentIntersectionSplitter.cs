using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Splits DXF polylines at all pairwise intersections while preserving
    /// the original point order along every polyline.
    /// </summary>
    internal sealed class DxfSegmentIntersectionSplitter
    {
        private readonly double _tolerance;

        internal DxfSegmentIntersectionSplitter(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<DxfPolyline> Split(List<DxfPolyline> segments)
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
                            if (distToInter < _tolerance)
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
                            if (distToInter < _tolerance)
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
                            if (dist < minDist && dist < _tolerance * 10) // Увеличиваем допуск для поиска
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

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;

            var dx = p1.X - p2.X;
            var dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= _tolerance;
        }
    }
}
