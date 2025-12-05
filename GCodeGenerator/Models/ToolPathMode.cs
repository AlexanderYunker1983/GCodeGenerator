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
        OnLine,
        
        /// <summary>
        /// Tool moves outside the contour.
        /// </summary>
        Outside,
        
        /// <summary>
        /// Tool moves inside the contour.
        /// </summary>
        Inside
    }
}

