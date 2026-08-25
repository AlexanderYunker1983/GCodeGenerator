#nullable enable
namespace GCodeGenerator.Models
{
    /// <summary>
    /// Spindle control settings (M3/M4/M5, S-code, spin-up delay).
    /// Пункт 8.1 плана: выделено из плоского <see cref="GCodeSettings"/>.
    /// Пункт 8.2 плана (D4): сериализуется в секцию "spindle" файла .ygc.
    /// </summary>
    public class SpindleSettings
    {
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
        ///
        /// В программу выводится как <c>G4 P&lt;миллисекунды&gt;</c>: 2 секунды
        /// дают <c>G4 P2000</c>. Так понимают аргумент P стойки Fanuc и
        /// совместимые с ними, но не все: в GRBL и LinuxCNC P задаётся
        /// в секундах, и та же строка означала бы паузу на полчаса. Выбор
        /// единицы принадлежит профилю станка, которого в программе пока нет
        /// (пункт R14 плана доработок); до его появления вывод рассчитан на
        /// Fanuc-совместимые стойки и зафиксирован golden-файлом
        /// <c>Drill.Points.SpindleDelay.nc</c>.
        /// </summary>
        public double SpindleDelaySeconds { get; set; } = 2.0;
    }
}
