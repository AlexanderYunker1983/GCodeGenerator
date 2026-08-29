#nullable enable
using System.Collections.Generic;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// A single movement segment of a tool trajectory (plan item 6.2).
    /// Pure data — no rendering types.
    /// </summary>
    public sealed class TrajectorySegment
    {
        /// <summary>Операция исходного документа; null у служебного перемещения.</summary>
        public object? Source { get; set; }

        /// <summary>Start point of the move.</summary>
        public Vec3 Start { get; set; }

        /// <summary>End point of the move.</summary>
        public Vec3 End { get; set; }

        /// <summary>Type of the move (G0/G1/G2/G3).</summary>
        public MoveType MoveType { get; set; }

        /// <summary>Arc center (G2/G3 with I/J/K offsets); null for non-arcs.</summary>
        public Vec3? ArcCenter { get; set; }

        /// <summary>Arc radius (G2/G3); 0 for non-arcs.</summary>
        public double ArcRadius { get; set; }

        /// <summary>
        /// Interpolated points of the arc (G2/G3); null for rapid/linear moves.
        /// The first and the last points are the arc's start and end.
        /// </summary>
        public IReadOnlyList<Vec3>? InterpolatedPoints { get; set; }
    }
}
