namespace GCodeGenerator.Models
{
    public class DrillHole
    {
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// Start Z coordinate (entry point).
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Total drilling depth (relative).
        /// </summary>
        public double TotalDepth { get; set; }

        /// <summary>
        /// Depth per pass.
        /// </summary>
        public double StepDepth { get; set; }

        /// <summary>
        /// Rapid move feed for Z (G0 equivalent, if controller uses feed).
        /// </summary>
        public double FeedZRapid { get; set; }

        /// <summary>
        /// Working feed for Z (G1).
        /// </summary>
        public double FeedZWork { get; set; }

        /// <summary>
        /// Retract height for drill after completing a hole.
        /// </summary>
        public double RetractHeight { get; set; }
    }
}


