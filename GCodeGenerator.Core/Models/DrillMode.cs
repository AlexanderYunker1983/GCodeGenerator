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
        Points,

        /// <summary>Holes along a straight line.</summary>
        Line,

        /// <summary>Rectangular grid of holes (all grid points).</summary>
        Array,

        /// <summary>Holes along the rectangle border only.</summary>
        Rect,

        /// <summary>Holes evenly spaced on a circle.</summary>
        Circle,

        /// <summary>Holes along a circular arc.</summary>
        Arc,

        /// <summary>Holes along the sides of a regular polygon.</summary>
        Polygon,

        /// <summary>Holes evenly spaced on an ellipse.</summary>
        Ellipse,

        /// <summary>Holes for a component package (DIP / TO / SOIC templates).</summary>
        Package
    }
}
