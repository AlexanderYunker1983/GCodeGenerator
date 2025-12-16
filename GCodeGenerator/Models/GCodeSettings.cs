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

        /// <summary>
        /// Enables dark (night) MahApps theme across the app.
        /// </summary>
        public bool UseDarkTheme { get; set; }

        /// <summary>
        /// Master flag: include spindle control commands in generated G-code.
        /// </summary>
        public bool SpindleControlEnabled { get; set; } = true;

        /// <summary>
        /// Emit spindle speed (S-code) before operations.
        /// </summary>
        public bool SpindleSpeedEnabled { get; set; } = true;

        /// <summary>
        /// Spindle speed value (RPM).
        /// </summary>
        public int SpindleSpeedRpm { get; set; } = 12000;

        /// <summary>
        /// Turn spindle on before operations.
        /// </summary>
        public bool SpindleStartEnabled { get; set; } = true;

        /// <summary>
        /// Spindle rotation command (M3 clockwise, M4 counter-clockwise).
        /// </summary>
        public string SpindleStartCommand { get; set; } = "M3";

        /// <summary>
        /// Turn spindle off after all operations (M5).
        /// </summary>
        public bool SpindleStopEnabled { get; set; } = true;

        /// <summary>
        /// Add delay after spindle start (G4).
        /// </summary>
        public bool SpindleDelayEnabled { get; set; } = false;

        /// <summary>
        /// Delay duration in seconds for spindle spin-up.
        /// </summary>
        public double SpindleDelaySeconds { get; set; } = 2.0;

        /// <summary>
        /// Master flag: include coolant commands (M8/M9) in generated G-code.
        /// </summary>
        public bool CoolantControlEnabled { get; set; } = true;

        /// <summary>
        /// Turn coolant on at program start (M8).
        /// </summary>
        public bool CoolantStartEnabled { get; set; } = true;

        /// <summary>
        /// Turn coolant off at program end (M9).
        /// </summary>
        public bool CoolantStopEnabled { get; set; } = true;

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
    }
}


