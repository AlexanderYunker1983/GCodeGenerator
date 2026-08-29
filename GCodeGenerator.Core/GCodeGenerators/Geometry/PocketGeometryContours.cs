#nullable enable
using System;
using System.Collections.Generic;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>Единый доступ к одной или нескольким границам кармана.</summary>
    internal static class PocketGeometryContours
    {
        public static IReadOnlyList<IContour> Get(
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));

            if (geometry is IMultiContourPocketGeometry multi)
                return multi.GetContours(toolRadius, taperOffset);

            var contour = geometry.GetContour(toolRadius, taperOffset);
            return contour == null
                ? Array.Empty<IContour>()
                : new[] { contour };
        }

        public static bool RequiresSafeTransitions(IPocketGeometry geometry)
            => geometry.RequiresSafeTransitions
                || geometry is IMultiContourPocketGeometry multi && multi.RequiresSafeTransitions;
    }
}
