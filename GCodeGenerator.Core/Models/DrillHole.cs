using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Отверстие сверления: координаты и параметры прохода по глубине.
    ///
    /// Значения по умолчанию совпадают с параметрами операции: отверстие,
    /// созданное без явных подач, всё равно годится для станка. Прежде они
    /// были нулевыми, и такое отверстие давало в программе <c>F0</c> —
    /// подачу, с которой инструмент никуда не поедет.
    /// </summary>
    public partial class DrillHole : ObservableObject
    {
        [ObservableProperty]
        private double _x;

        [ObservableProperty]
        private double _y;

        /// <summary>
        /// Start Z coordinate (entry point).
        /// </summary>
        [ObservableProperty]
        private double _z;

        /// <summary>
        /// Total drilling depth (relative).
        /// </summary>
        [ObservableProperty]
        private double _totalDepth = 2.0;

        /// <summary>
        /// Depth per pass.
        /// </summary>
        [ObservableProperty]
        private double _stepDepth = 1.0;

        /// <summary>
        /// Rapid move feed for Z (G0 equivalent, if controller uses feed).
        /// </summary>
        [ObservableProperty]
        private double _feedZRapid = 500.0;

        /// <summary>
        /// Working feed for Z (G1).
        /// </summary>
        [ObservableProperty]
        private double _feedZWork = 200.0;

        /// <summary>
        /// Retract height for drill after completing a hole.
        /// </summary>
        [ObservableProperty]
        private double _retractHeight = 0.3;
    }
}
