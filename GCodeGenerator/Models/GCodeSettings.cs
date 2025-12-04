namespace GCodeGenerator.Models
{
    /// <summary>
    /// Settings that influence generated g-code formatting and used commands.
    /// </summary>
    public class GCodeSettings
    {
        public bool UseLineNumbers { get; set; } = true;

        public int LineNumberStart { get; set; } = 10;

        public int LineNumberStep { get; set; } = 10;

        public bool UseComments { get; set; } = true;

        /// <summary>
        /// Allow arc moves (G2/G3). If false, arcs must be converted to linear moves.
        /// </summary>
        public bool AllowArcs { get; set; } = true;

        /// <summary>
        /// If true, G-codes are formatted with leading zero, e.g. G01 instead of G1.
        /// </summary>
        public bool UsePaddedGCodes { get; set; } = false;
    }
}


