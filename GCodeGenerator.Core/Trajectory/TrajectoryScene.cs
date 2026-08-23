using System;
using System.Collections.Generic;

namespace GCodeGenerator.Trajectory
{
    /// <summary>
    /// A pure scene of a tool trajectory (plan item 6.2): segments only,
    /// no rendering types. Built by <see cref="SceneBuilder"/> from a
    /// structured <see cref="GCodeProgram"/>; rendered to WPF by the
    /// Views layer (<c>SceneRenderer</c>).
    /// </summary>
    public sealed class TrajectoryScene
    {
        public TrajectoryScene(IReadOnlyList<TrajectorySegment> segments)
        {
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
        }

        /// <summary>An empty scene (no segments).</summary>
        public static TrajectoryScene Empty { get; } = new TrajectoryScene(Array.Empty<TrajectorySegment>());

        /// <summary>Trajectory segments in program order.</summary>
        public IReadOnlyList<TrajectorySegment> Segments { get; }

        public bool IsEmpty => Segments.Count == 0;

        /// <summary>
        /// Bounds of all points (interpolated arc points or segment ends).
        /// Null for an empty scene.
        /// </summary>
        public (Vec3 Min, Vec3 Max)? Bounds
        {
            get
            {
                double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
                bool any = false;

                void Add(Vec3 p)
                {
                    if (!any)
                    {
                        minX = maxX = p.X;
                        minY = maxY = p.Y;
                        minZ = maxZ = p.Z;
                        any = true;
                        return;
                    }

                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.Z < minZ) minZ = p.Z;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                    if (p.Z > maxZ) maxZ = p.Z;
                }

                foreach (var segment in Segments)
                {
                    if (segment.InterpolatedPoints != null && segment.InterpolatedPoints.Count > 0)
                    {
                        foreach (var point in segment.InterpolatedPoints)
                            Add(point);
                    }
                    else
                    {
                        Add(segment.Start);
                        Add(segment.End);
                    }
                }

                return any
                    ? (new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ))
                    : null;
            }
        }
    }
}
