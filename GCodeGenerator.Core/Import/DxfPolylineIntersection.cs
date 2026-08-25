using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    internal sealed class DxfPolylineIntersection
    {
        internal DxfPolylineIntersection(DxfPoint point, double distance)
        {
            Point = point;
            Distance = distance;
        }

        internal DxfPoint Point { get; }

        internal double Distance { get; }
    }
}
