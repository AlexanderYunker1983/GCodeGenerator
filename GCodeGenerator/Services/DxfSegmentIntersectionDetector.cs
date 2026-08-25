using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Calculates unique intersections between polyline segments and common
    /// point-to-segment distances.
    /// </summary>
    internal sealed class DxfSegmentIntersectionDetector
    {
        private readonly double _tolerance;

        internal DxfSegmentIntersectionDetector(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<DxfPoint> FindIntersections(DxfPolyline seg1, DxfPolyline seg2)
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
            if (Math.Abs(denom) < GeometryTolerances.Degenerate)
                return null; // Параллельные линии
            
            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;
            
            // Используем небольшой допуск для границ отрезков
            const double tolerance = GeometryTolerances.Vertex;
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

        internal double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < GeometryTolerances.Degenerate && Math.Abs(dy) < GeometryTolerances.Degenerate)
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
