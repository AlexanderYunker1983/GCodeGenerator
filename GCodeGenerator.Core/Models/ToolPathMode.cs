#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Tool path mode for profile milling operations.
    /// </summary>
    public enum ToolPathMode
    {
        /// <summary>
        /// Tool moves along the contour line.
        /// </summary>
        OnLine = 0,
        
        /// <summary>
        /// Tool moves outside the contour.
        /// </summary>
        Outside = 1,
        
        /// <summary>
        /// Tool moves inside the contour.
        /// </summary>
        Inside = 2
    }
}
