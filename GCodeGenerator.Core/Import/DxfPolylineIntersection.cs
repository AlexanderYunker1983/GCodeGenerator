using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    internal sealed class DxfPolylineIntersection
    {
        internal DxfPolylineIntersection(Point2D point, double distance)
        {
            Point = point;
            Distance = distance;
        }

        internal Point2D Point { get; }

        internal double Distance { get; }
    }
}
