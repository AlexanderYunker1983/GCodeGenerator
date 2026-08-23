using System.Collections.Generic;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drilling holes operation with individual hole Z-parameters
    /// and common XY feeds & safety settings.
    /// </summary>
    public class DrillPointsOperation : OperationBase
    {
        public DrillPointsOperation() : base(OperationType.DrillPoints, "Drill points")
        {
            Metadata = new Dictionary<string, object>();
        }

        /// <summary>
        /// Holes with full coordinates and Z parameters.
        /// Setter is needed for JSON deserialization of saved projects.
        /// </summary>
        public List<DrillHole> Holes { get; set; } = new List<DrillHole>();

        /// <summary>
        /// Rapid feed in XY plane (G0).
        /// </summary>
        public double FeedXYRapid { get; set; } = 1000.0;

        /// <summary>
        /// Working feed in XY plane (G1).
        /// </summary>
        public double FeedXYWork { get; set; } = 300.0;

        /// <summary>
        /// Safe Z height for moves between holes.
        /// </summary>
        public double SafeZBetweenHoles { get; set; } = 1.0;

        /// <summary>
        /// Number of decimal places for coordinates.
        /// </summary>
        public int Decimals { get; set; } = 3;

        /// <summary>
        /// Metadata for storing operation-specific parameters (e.g., line distance, array dimensions, circle radius, etc.)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        public override string GetDescription()
        {
            return $"Drill {Holes.Count} hole(s)";
        }
    }
}


