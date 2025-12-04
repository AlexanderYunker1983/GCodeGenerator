using System.Collections.Generic;
using System.Windows;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Drilling holes in specified XY points with the same depth and tool.
    /// </summary>
    public class DrillPointsOperation : OperationBase
    {
        public DrillPointsOperation() : base(OperationType.DrillPoints, "Drill points")
        {
        }

        /// <summary>
        /// Points in workpiece coordinates (X, Y).
        /// </summary>
        public List<Point> Points { get; } = new List<Point>();

        public double SafeZ { get; set; } = 5.0;

        public double DrillZ { get; set; } = -2.0;

        public double Feed { get; set; } = 100.0;

        public override string GetDescription()
        {
            return $"Drill {Points.Count} point(s) to Z={DrillZ}";
        }
    }
}


