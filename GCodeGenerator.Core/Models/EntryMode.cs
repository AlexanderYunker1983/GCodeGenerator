#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Tool entry mode for milling operations.
    /// </summary>
    public enum EntryMode
    {
        /// <summary>
        /// Vertical entry (plunge).
        /// </summary>
        Vertical = 0,
        
        /// <summary>
        /// Angled entry (ramp).
        /// </summary>
        Angled = 1
    }
}
