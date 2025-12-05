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
        Vertical,
        
        /// <summary>
        /// Angled entry (ramp).
        /// </summary>
        Angled
    }
}

