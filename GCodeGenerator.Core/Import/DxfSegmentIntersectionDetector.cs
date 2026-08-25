using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
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

        internal List<Point2D> FindIntersections(Polyline2D seg1, Polyline2D seg2)
        {
            var intersections = new List<Point2D>();
            
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

        private Point2D FindLineSegmentIntersection(double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4)
            => Geometry2D.SegmentIntersectionPoint(
                x1, y1, x2, y2, x3, y3, x4, y4,
                GeometryTolerances.Degenerate,
                GeometryTolerances.Vertex);

        internal double DistanceToSegment(double px, double py, double x1, double y1, double x2, double y2)
            => Geometry2D.DistanceToSegment(px, py, x1, y1, x2, y2, GeometryTolerances.Degenerate);

        private bool PointsMatch(Point2D p1, Point2D p2)
            => Geometry2D.PointsMatch(p1, p2, _tolerance);
    }
}
