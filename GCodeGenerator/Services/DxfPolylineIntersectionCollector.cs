using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Collects unique pairwise intersections for every polyline and orders
    /// them by travelled distance from that polyline's first point.
    /// </summary>
    internal sealed class DxfPolylineIntersectionCollector
    {
        private readonly double _tolerance;
        private readonly DxfSegmentIntersectionDetector _detector;

        internal DxfPolylineIntersectionCollector(
            double tolerance,
            DxfSegmentIntersectionDetector detector)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        }

        internal Dictionary<int, List<DxfPolylineIntersection>> Collect(
            IReadOnlyList<DxfPolyline> polylines)
        {
            var result = new Dictionary<int, List<DxfPolylineIntersection>>();

            for (var firstIndex = 0; firstIndex < polylines.Count; firstIndex++)
            {
                var first = polylines[firstIndex];
                if (!IsValid(first))
                    continue;

                EnsureEntry(result, firstIndex);
                for (var secondIndex = firstIndex + 1; secondIndex < polylines.Count; secondIndex++)
                {
                    var second = polylines[secondIndex];
                    if (!IsValid(second))
                        continue;

                    foreach (var point in _detector.FindIntersections(first, second))
                    {
                        AddUnique(
                            result[firstIndex],
                            point,
                            DistanceAlongPolyline(first, point));

                        EnsureEntry(result, secondIndex);
                        AddUnique(
                            result[secondIndex],
                            point,
                            DistanceAlongPolyline(second, point));
                    }
                }
            }

            foreach (var intersections in result.Values)
                intersections.Sort((left, right) => left.Distance.CompareTo(right.Distance));

            return result;
        }

        private double DistanceAlongPolyline(DxfPolyline polyline, DxfPoint point)
        {
            var distance = 0.0;
            for (var index = 0; index < polyline.Points.Count - 1; index++)
            {
                var start = polyline.Points[index];
                var end = polyline.Points[index + 1];
                var segmentLength = Distance(start, end);
                if (_detector.DistanceToSegment(
                        point.X,
                        point.Y,
                        start.X,
                        start.Y,
                        end.X,
                        end.Y) < _tolerance)
                {
                    return distance + Distance(start, point);
                }

                distance += segmentLength;
            }

            return distance;
        }

        private void AddUnique(
            List<DxfPolylineIntersection> intersections,
            DxfPoint point,
            double distance)
        {
            if (!intersections.Any(item => PointsMatch(item.Point, point)))
                intersections.Add(new DxfPolylineIntersection(point, distance));
        }

        private static void EnsureEntry(
            IDictionary<int, List<DxfPolylineIntersection>> result,
            int index)
        {
            if (!result.ContainsKey(index))
                result[index] = new List<DxfPolylineIntersection>();
        }

        private static bool IsValid(DxfPolyline polyline)
            => polyline?.Points != null && polyline.Points.Count >= 2;

        private static double Distance(DxfPoint first, DxfPoint second)
            => Geometry2D.Distance(first, second);

        private bool PointsMatch(DxfPoint first, DxfPoint second)
            => Geometry2D.PointsMatch(first, second, _tolerance);
    }
}
