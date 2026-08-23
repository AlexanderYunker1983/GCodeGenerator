using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Kind of a shape in the 2D operations preview scene (plan item 6.3).
    /// </summary>
    public enum OperationShapeKind
    {
        /// <summary>A single point (drill hole).</summary>
        Point,

        /// <summary>A polyline / closed contour (profile or pocket).</summary>
        Contour
    }

    /// <summary>
    /// A single shape of the 2D operations preview scene (plan item 6.3).
    /// Pure data — no WPF types.
    /// </summary>
    public sealed class OperationShape
    {
        public OperationShape(OperationBase operation, OperationShapeKind kind,
            IReadOnlyList<(double X, double Y)> points, bool isClosed, bool isFilled)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Kind = kind;
            Points = points ?? throw new ArgumentNullException(nameof(points));
            IsClosed = isClosed;
            IsFilled = isFilled;
        }

        /// <summary>The operation this shape belongs to (selection, hover, tooltip).</summary>
        public OperationBase Operation { get; }

        public OperationShapeKind Kind { get; }

        /// <summary>Points in program coordinates (mm).</summary>
        public IReadOnlyList<(double X, double Y)> Points { get; }

        /// <summary>Whether the contour is closed (first point == last point).</summary>
        public bool IsClosed { get; }

        /// <summary>Whether the shape is drawn filled (pockets).</summary>
        public bool IsFilled { get; }
    }

    /// <summary>
    /// A pure 2D scene of all operations (plan item 6.3): shapes only, no
    /// WPF types. Built by <see cref="OperationSceneBuilder"/>; rendered by
    /// the Views layer.
    /// </summary>
    public sealed class OperationScene
    {
        public OperationScene(IReadOnlyList<OperationShape> shapes)
        {
            Shapes = shapes ?? throw new ArgumentNullException(nameof(shapes));
        }

        /// <summary>An empty scene (no shapes).</summary>
        public static OperationScene Empty { get; } = new OperationScene(Array.Empty<OperationShape>());

        public IReadOnlyList<OperationShape> Shapes { get; }

        public bool IsEmpty => Shapes.Count == 0;

        /// <summary>Bounds of all shape points; null for an empty scene.</summary>
        public (double MinX, double MinY, double MaxX, double MaxY)? Bounds
        {
            get
            {
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                bool any = false;

                foreach (var shape in Shapes)
                {
                    foreach (var (x, y) in shape.Points)
                    {
                        if (!any)
                        {
                            minX = maxX = x;
                            minY = maxY = y;
                            any = true;
                            continue;
                        }

                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                return any ? (minX, minY, maxX, maxY) : null;
            }
        }
    }
}
