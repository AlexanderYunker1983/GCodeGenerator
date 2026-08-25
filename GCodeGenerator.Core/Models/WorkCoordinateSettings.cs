#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Work coordinate system settings (G54-G59, G92 start, end position).
    /// Пункт 8.1 плана: выделено из плоского <see cref="GCodeSettings"/>.
    /// </summary>
    public class WorkCoordinateSettings
    {
        /// <summary>
        /// If true, adds a G92 command at the very beginning of program
        /// that sets the current position to the specified start coordinates.
        /// </summary>
        public bool AddStartPosition { get; set; } = false;

        public double StartX { get; set; } = 0.0;
        public double StartY { get; set; } = 0.0;
        public double StartZ { get; set; } = 0.0;

        /// <summary>
        /// If true, moves to specified coordinates at the end of program using rapid move (G0).
        /// </summary>
        public bool AddEndPosition { get; set; } = false;

        public double EndX { get; set; } = 0.0;
        public double EndY { get; set; } = 0.0;
        public double EndZ { get; set; } = 0.0;

        /// <summary>
        /// If true, sets the work coordinate system (G54-G59) at the beginning of the program.
        /// </summary>
        public bool SetWorkCoordinateSystem { get; set; } = false;

        /// <summary>
        /// Work coordinate system to use (G54, G55, G56, G57, G58, G59).
        /// </summary>
        public string WorkCoordinateSystem { get; set; } = "G54";
    }
}
