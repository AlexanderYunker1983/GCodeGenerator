#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Reference point type for rectangle operations.
    /// </summary>
    public enum ReferencePointType
    {
        /// <summary>
        /// Center of rectangle.
        /// </summary>
        Center = 0,
        
        /// <summary>
        /// Top-left corner.
        /// </summary>
        TopLeft = 1,
        
        /// <summary>
        /// Top-right corner.
        /// </summary>
        TopRight = 2,
        
        /// <summary>
        /// Bottom-left corner.
        /// </summary>
        BottomLeft = 3,
        
        /// <summary>
        /// Bottom-right corner.
        /// </summary>
        BottomRight = 4
    }
}
