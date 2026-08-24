namespace GCodeGenerator.Models
{
    /// <summary>
    /// Aggregate of all settings that influence the application and generated
    /// g-code. Пункт 8.1 плана: плоский класс на 28 свойств разбит на
    /// тематические группы — <see cref="Format"/>, <see cref="Spindle"/>,
    /// <see cref="Coolant"/>, <see cref="WorkCoordinate"/> и <see cref="Ui"/>.
    /// </summary>
    public class GCodeSettings
    {
        /// <summary>G-code formatting (line numbers, comments, arcs, padded G-codes).</summary>
        public GCodeFormatSettings Format { get; set; } = new GCodeFormatSettings();

        /// <summary>Spindle control (M3/M4/M5, S-code, spin-up delay).</summary>
        public SpindleSettings Spindle { get; set; } = new SpindleSettings();

        /// <summary>Coolant control (M8/M9).</summary>
        public CoolantSettings Coolant { get; set; } = new CoolantSettings();

        /// <summary>Work coordinate system (G54-G59, G92 start, end position).</summary>
        public WorkCoordinateSettings WorkCoordinate { get; set; } = new WorkCoordinateSettings();

        /// <summary>UI settings (theme).</summary>
        public UiSettings Ui { get; set; } = new UiSettings();
    }
}
