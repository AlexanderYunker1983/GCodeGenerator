#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Coolant control settings (M8/M9).
    /// Пункт 8.1 плана: выделено из плоского <see cref="GCodeSettings"/>.
    /// Пункт 8.2 плана (D4): сериализуется в секцию "coolant" файла .ygc.
    /// </summary>
    public class CoolantSettings
    {
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
    }
}
