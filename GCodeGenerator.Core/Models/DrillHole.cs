using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
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
        private double _totalDepth;

        /// <summary>
        /// Depth per pass.
        /// </summary>
        [ObservableProperty]
        private double _stepDepth;

        /// <summary>
        /// Rapid move feed for Z (G0 equivalent, if controller uses feed).
        /// </summary>
        [ObservableProperty]
        private double _feedZRapid;

        /// <summary>
        /// Working feed for Z (G1).
        /// </summary>
        [ObservableProperty]
        private double _feedZWork;

        /// <summary>
        /// Retract height for drill after completing a hole.
        /// </summary>
        [ObservableProperty]
        private double _retractHeight;
    }
}


