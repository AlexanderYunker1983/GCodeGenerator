using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Inserts collected intersection points into their source polylines and
    /// emits an ordered set of two-point graph edges.
    /// </summary>
    internal sealed class DxfPolylineSubdivider
    {
        private readonly double _tolerance;
        private readonly DxfSegmentIntersectionDetector _detector;

        internal DxfPolylineSubdivider(
            double tolerance,
            DxfSegmentIntersectionDetector detector)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        }

        internal List<Polyline2D> Subdivide(
            IReadOnlyList<Polyline2D> polylines,
            IReadOnlyDictionary<int, List<DxfPolylineIntersection>> intersectionMap)
        {
            var result = new List<Polyline2D>();

            for (var polylineIndex = 0; polylineIndex < polylines.Count; polylineIndex++)
            {
                var polyline = polylines[polylineIndex];
                if (polyline?.Points == null || polyline.Points.Count < 2)
                    continue;

                if (!intersectionMap.TryGetValue(polylineIndex, out var intersections))
                {
                    result.Add(polyline);
                    continue;
                }

                var points = new List<Point2D>(polyline.Points);
                foreach (var intersection in intersections)
                    InsertIntersection(points, intersection.Point);

                for (var pointIndex = 0; pointIndex < points.Count - 1; pointIndex++)
                {
                    result.Add(new Polyline2D
                    {
                        Points = new List<Point2D>
                        {
                            points[pointIndex],
                            points[pointIndex + 1],
                        },
                    });
                }
            }

            return result;
        }

        private void InsertIntersection(List<Point2D> points, Point2D intersection)
        {
            var insertPosition = -1;
            var minimumDistance = double.MaxValue;

            for (var index = 0; index < points.Count - 1; index++)
            {
                var start = points[index];
                var end = points[index + 1];
                var distance = _detector.DistanceToSegment(
                    intersection.X,
                    intersection.Y,
                    start.X,
                    start.Y,
                    end.X,
                    end.Y);
                if (distance >= minimumDistance || distance >= _tolerance * 10)
                    continue;

                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var lengthSquared = dx * dx + dy * dy;
                if (lengthSquared <= 1e-18)
                    continue;

                var position = ((intersection.X - start.X) * dx +
                                (intersection.Y - start.Y) * dy) / lengthSquared;
                if (position < -0.01 || position > 1.01)
                    continue;

                minimumDistance = distance;
                insertPosition = index + 1;
            }

            if (insertPosition < 0 || HasNearbyPoint(points, insertPosition, intersection))
                return;

            points.Insert(insertPosition, intersection);
        }

        private bool HasNearbyPoint(
            IReadOnlyList<Point2D> points,
            int insertPosition,
            Point2D candidate)
        {
            var start = Math.Max(0, insertPosition - 1);
            var end = Math.Min(points.Count, insertPosition + 2);
            for (var index = start; index < end; index++)
            {
                if (PointsMatch(points[index], candidate))
                    return true;
            }

            return false;
        }

        private bool PointsMatch(Point2D first, Point2D second)
            => Geometry2D.PointsMatch(first, second, _tolerance);
    }
}
