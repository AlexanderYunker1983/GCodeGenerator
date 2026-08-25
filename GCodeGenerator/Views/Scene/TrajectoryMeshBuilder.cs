using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using GCodeGenerator.Trajectory;

namespace GCodeGenerator.Views.Scene
{
    /// <summary>Роль точки программы: начало, смена типа перемещения, конец.</summary>
    internal enum MarkerRole
    {
        Start,
        Transition,
        End
    }

    /// <summary>Шарик-маркер в трёхмерной сцене.</summary>
    internal readonly struct SceneMarker
    {
        public SceneMarker(Point3D position, double radius, MarkerRole role)
        {
            Position = position;
            Radius = radius;
            Role = role;
        }

        public Point3D Position { get; }

        public double Radius { get; }

        public MarkerRole Role { get; }
    }

    /// <summary>
    /// Готовая геометрия траектории: по одному мешу на тип перемещения плюс
    /// размеры, привязанные к габаритам программы.
    /// </summary>
    internal sealed class TrajectoryMeshes
    {
        /// <summary>Холостые перемещения (штриховые линии).</summary>
        public MeshGeometry3D Rapid { get; } = new MeshGeometry3D();

        /// <summary>Рабочие линейные перемещения.</summary>
        public MeshGeometry3D Linear { get; } = new MeshGeometry3D();

        /// <summary>Дуги по часовой стрелке.</summary>
        public MeshGeometry3D ArcCW { get; } = new MeshGeometry3D();

        /// <summary>Дуги против часовой стрелки.</summary>
        public MeshGeometry3D ArcCCW { get; } = new MeshGeometry3D();

        /// <summary>Маркеры ключевых точек программы.</summary>
        public IReadOnlyList<SceneMarker> Markers { get; set; } = Array.Empty<SceneMarker>();

        /// <summary>Толщина линии траектории, подобранная под габариты программы.</summary>
        public double LineThickness { get; set; }

        /// <summary>Длина плеча координатных осей.</summary>
        public double AxisLength { get; set; }

        /// <summary>Длина наконечника оси.</summary>
        public double ArrowLength { get; set; }

        /// <summary>Радиус наконечника оси.</summary>
        public double ArrowRadius { get; set; }
    }

    /// <summary>
    /// Собирает траекторию в меши: тысячи отрезков программы группируются по
    /// типу перемещения, поэтому сцена состоит из четырёх объектов вместо
    /// одного на каждый отрезок.
    ///
    /// Класс не знает ни о цветах, ни об источниках света — только о
    /// геометрии, поэтому проверяется тестами без окна.
    /// </summary>
    internal static class TrajectoryMeshBuilder
    {
        /// <summary>Наименьшая длина штриха и промежутка холостого хода, мм.</summary>
        private const double MinimumDashLength = 2.0;
        private const double MinimumGapLength = 1.5;

        /// <summary>Толщина линии не опускается ниже этого значения, мм.</summary>
        private const double MinimumLineThickness = 0.05;

        /// <summary>Габарит программы, ниже которого размеры не уменьшаются, мм.</summary>
        private const double MinimumExtent = 1.0;

        /// <summary>Длина осей для пустой программы, мм.</summary>
        private const double DefaultAxisLength = 10.0;
        private const double DefaultArrowLength = 1.5;

        public static TrajectoryMeshes Build(TrajectoryScene scene)
        {
            var segments = ToRenderSegments(scene);
            var meshes = new TrajectoryMeshes();

            var extent = Extent(scene);
            meshes.LineThickness = Math.Max(extent * 0.008 * 0.4, MinimumLineThickness);
            meshes.AxisLength = segments.Count > 0 ? Math.Max(extent * 0.6, DefaultAxisLength) : DefaultAxisLength;
            meshes.ArrowLength = segments.Count > 0 ? meshes.AxisLength * 0.12 : DefaultArrowLength;
            meshes.ArrowRadius = meshes.LineThickness * 2;

            if (segments.Count == 0)
                return meshes;

            var dashLength = Math.Max(extent * 0.03, MinimumDashLength);
            var gapLength = Math.Max(extent * 0.02, MinimumGapLength);

            foreach (var segment in segments)
            {
                if (segment.MoveType == MoveType.Rapid)
                {
                    SceneGeometry.AddDashedLine(meshes.Rapid, segment.Start, segment.End,
                        meshes.LineThickness, dashLength, gapLength);
                    continue;
                }

                var target = segment.MoveType switch
                {
                    MoveType.ArcCW => meshes.ArcCW,
                    MoveType.ArcCCW => meshes.ArcCCW,
                    _ => meshes.Linear
                };

                // Дуга приходит уже разложенной на точки: рисуется ломаной.
                if (segment.InterpolatedPoints != null && segment.InterpolatedPoints.Count > 1)
                {
                    for (var i = 0; i < segment.InterpolatedPoints.Count - 1; i++)
                    {
                        SceneGeometry.AddLine(target, segment.InterpolatedPoints[i],
                            segment.InterpolatedPoints[i + 1], meshes.LineThickness);
                    }
                }
                else
                {
                    SceneGeometry.AddLine(target, segment.Start, segment.End, meshes.LineThickness);
                }
            }

            meshes.Markers = BuildMarkers(segments, meshes.LineThickness * 2);
            return meshes;
        }

        /// <summary>
        /// Маркеры: первая точка программы, точки смены типа перемещения и
        /// последняя точка. Начало и конец крупнее промежуточных.
        /// </summary>
        private static List<SceneMarker> BuildMarkers(List<RenderSegment> segments, double markerRadius)
        {
            var points = new List<Point3D> { segments[0].Start };
            var lastMoveType = segments[0].MoveType;

            foreach (var segment in segments)
            {
                if (segment.MoveType == lastMoveType)
                    continue;

                points.Add(segment.Start);
                lastMoveType = segment.MoveType;
            }

            points.Add(segments[segments.Count - 1].End);

            var markers = new List<SceneMarker>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                if (i == 0)
                    markers.Add(new SceneMarker(points[i], markerRadius * 1.5, MarkerRole.Start));
                else if (i == points.Count - 1)
                    markers.Add(new SceneMarker(points[i], markerRadius * 1.3, MarkerRole.End));
                else
                    markers.Add(new SceneMarker(points[i], markerRadius * 0.8, MarkerRole.Transition));
            }

            return markers;
        }

        /// <summary>
        /// Наибольший габарит программы. От него зависят толщина линий, длина
        /// штриха и осей: одна и та же сцена должна читаться и на детали
        /// в десять миллиметров, и на плите в метр.
        /// </summary>
        private static double Extent(TrajectoryScene scene)
        {
            var bounds = scene?.Bounds;
            if (bounds == null)
                return MinimumExtent;

            var (min, max) = bounds.Value;
            return Math.Max(Math.Max(max.X - min.X, max.Y - min.Y), Math.Max(max.Z - min.Z, MinimumExtent));
        }

        private static List<RenderSegment> ToRenderSegments(TrajectoryScene scene)
        {
            var result = new List<RenderSegment>();
            if (scene == null)
                return result;

            foreach (var segment in scene.Segments)
            {
                List<Point3D> interpolated = null;
                if (segment.InterpolatedPoints != null)
                {
                    interpolated = new List<Point3D>(segment.InterpolatedPoints.Count);
                    foreach (var point in segment.InterpolatedPoints)
                        interpolated.Add(ToPoint3D(point));
                }

                result.Add(new RenderSegment(
                    ToPoint3D(segment.Start), ToPoint3D(segment.End), segment.MoveType, interpolated));
            }

            return result;
        }

        private static Point3D ToPoint3D(Vec3 v) => new Point3D(v.X, v.Y, v.Z);

        /// <summary>Отрезок сцены в координатах WPF — граница между ядром и представлением.</summary>
        private sealed class RenderSegment
        {
            public RenderSegment(Point3D start, Point3D end, MoveType moveType, List<Point3D> interpolatedPoints)
            {
                Start = start;
                End = end;
                MoveType = moveType;
                InterpolatedPoints = interpolatedPoints;
            }

            public Point3D Start { get; }

            public Point3D End { get; }

            public MoveType MoveType { get; }

            public List<Point3D> InterpolatedPoints { get; }
        }
    }
}
