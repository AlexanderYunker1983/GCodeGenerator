#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drill pattern of a <see cref="DrillPointsOperation"/> (plan item 3.1).
    /// Replaces the legacy pattern detection by <c>Metadata</c> keys
    /// (presence of "StartX" / "CenterX" / "PackageName", etc.).
    /// </summary>
    public enum DrillMode
    {
        /// <summary>Individual holes from the <see cref="DrillPointsOperation.Holes"/> list.</summary>
        Points = 0,

        /// <summary>Holes along a straight line.</summary>
        Line = 1,

        /// <summary>Rectangular grid of holes (all grid points).</summary>
        Array = 2,

        /// <summary>Holes along the rectangle border only.</summary>
        Rect = 3,

        /// <summary>Holes evenly spaced on a circle.</summary>
        Circle = 4,

        /// <summary>Holes along a circular arc.</summary>
        Arc = 5,

        /// <summary>Holes along the sides of a regular polygon.</summary>
        Polygon = 6,

        /// <summary>Holes evenly spaced on an ellipse.</summary>
        Ellipse = 7,

        /// <summary>Holes for a component package (DIP / TO / SOIC templates).</summary>
        Package = 8
    }
}
