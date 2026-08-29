#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Connects ordered or reversed DXF segments whose endpoints match within
    /// the configured tolerance.
    /// </summary>
    internal sealed class DxfSegmentConnector
    {
        private readonly double _tolerance;

        internal DxfSegmentConnector(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<Polyline2D> Connect(List<Polyline2D> segments)
        {
            var contours = new List<Polyline2D>();
            var used = new bool[segments.Count];
            var endpoints = BuildEndpointIndex(segments);
            
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                    continue;
                
                // Пытаемся построить контур, начиная с этого сегмента
                var contour = BuildContourFromSegment(segments, i, used, endpoints);
                if (contour != null && contour.Points != null && contour.Points.Count >= 3)
                {
                    contours.Add(contour);
                }
            }
            
            return contours;
        }

        private Polyline2D BuildContourFromSegment(List<Polyline2D> segments, int startIdx,
            bool[] used, SpatialPointIndex<SegmentEndpoint> endpoints)
        {
            var contourPoints = new List<Point2D>();
            var startPoint = segments[startIdx].Points[0];
            var currentPoint = segments[startIdx].Points[segments[startIdx].Points.Count - 1];
            
            // Добавляем точки первого сегмента
            foreach (var p in segments[startIdx].Points)
            {
                contourPoints.Add(new Point2D { X = p.X, Y = p.Y });
            }
            used[startIdx] = true;
            
            // Ищем следующий сегмент, который начинается там, где заканчивается текущий
            while (true)
            {
                if (!endpoints.TryFindFirst(currentPoint, endpoint => !used[endpoint.Index],
                        out var connection))
                    break; // Не нашли следующий сегмент
                
                // Добавляем точки следующего сегмента
                var nextSeg = segments[connection.Index];
                if (connection.Reverse)
                {
                    // Добавляем точки в обратном порядке
                    for (int j = nextSeg.Points.Count - 2; j >= 0; j--) // Пропускаем последнюю точку (она уже есть)
                    {
                        contourPoints.Add(new Point2D { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[0];
                }
                else
                {
                    // Добавляем точки в прямом порядке
                    for (int j = 1; j < nextSeg.Points.Count; j++) // Пропускаем первую точку (она уже есть)
                    {
                        contourPoints.Add(new Point2D { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[nextSeg.Points.Count - 1];
                }
                
                used[connection.Index] = true;
                
                // Проверяем, замкнулся ли контур
                if (PointsMatch(currentPoint, startPoint))
                {
                    break; // Контур замкнут
                }
            }
            
            return new Polyline2D { Points = contourPoints };
        }

        private SpatialPointIndex<SegmentEndpoint> BuildEndpointIndex(
            IReadOnlyList<Polyline2D> segments)
        {
            var index = new SpatialPointIndex<SegmentEndpoint>(_tolerance);
            for (var i = 0; i < segments.Count; i++)
            {
                var points = segments[i]?.Points;
                if (points == null || points.Count < 2)
                    continue;

                index.Add(points[0], new SegmentEndpoint(i, false));
                index.Add(points[points.Count - 1], new SegmentEndpoint(i, true));
            }

            return index;
        }

        private readonly struct SegmentEndpoint
        {
            internal SegmentEndpoint(int index, bool reverse)
            {
                Index = index;
                Reverse = reverse;
            }

            internal int Index { get; }
            internal bool Reverse { get; }
        }

        private bool PointsMatch(Point2D p1, Point2D p2)
            => Geometry2D.PointsMatch(p1, p2, _tolerance);
    }
}
