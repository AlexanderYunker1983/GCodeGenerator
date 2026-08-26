#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Splits DXF polylines at all pairwise intersections while preserving
    /// the original point order along every polyline.
    /// </summary>
    internal sealed class DxfSegmentIntersectionSplitter
    {
        private readonly DxfPolylineIntersectionCollector _intersectionCollector;
        private readonly DxfPolylineSubdivider _subdivider;

        internal DxfSegmentIntersectionSplitter(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            var detector = new DxfSegmentIntersectionDetector(tolerance);
            _intersectionCollector = new DxfPolylineIntersectionCollector(tolerance, detector);
            _subdivider = new DxfPolylineSubdivider(tolerance, detector);
        }

        internal List<Polyline2D> Split(List<Polyline2D> segments)
        {
            var intersections = _intersectionCollector.Collect(segments);
            return _subdivider.Subdivide(segments, intersections);
        }
    }
}
