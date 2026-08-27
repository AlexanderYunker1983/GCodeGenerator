#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Допустимая область движения центра фрезы после вычитания островов.
    /// Clipper2 хранит её как иерархию внешних контуров и отверстий; повторное
    /// смещение нужно концентрической стратегии, которая идёт всё глубже от
    /// обеих стенок — внешней границы кармана и границ островов.
    /// </summary>
    internal sealed class PocketRegionGeometry : IPocketGeometry, IMultiContourPocketGeometry
    {
        private const int Precision = 6;
        private const double MiterLimit = 2.0;

        private readonly PathsD _paths;
        private bool _hasCachedTree;
        private double _cachedOffset;
        private PolyTreeD? _cachedTree;

        private PocketRegionGeometry(PathsD paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        public bool SplitsIntoAreas => true;

        public bool RequiresSafeTransitions => true;

        /// <summary>
        /// Возвращает область только когда хотя бы один остров действительно
        /// пересекает текущий слой. Так остров вдали от кармана не меняет ни
        /// порядок точек, ни прежний G-code операции.
        /// </summary>
        public static PocketRegionGeometry? TryCreate(
            IPocketGeometry pocket,
            IReadOnlyList<PocketOperationBase> islands,
            double contourOffset,
            double taperOffset)
        {
            if (pocket == null)
                throw new ArgumentNullException(nameof(pocket));
            if (islands == null || islands.Count == 0)
                return null;

            var subject = GeometryPaths(pocket, contourOffset, taperOffset);
            if (subject.Count == 0)
                return new PocketRegionGeometry(subject);

            var clips = new PathsD();
            foreach (var island in islands)
            {
                if (island == null)
                    continue;

                // Центр инструмента не может приблизиться к физической
                // границе острова ближе радиуса фрезы вместе с припуском.
                var islandGeometry = OperationCatalog.CreatePocketGeometry(island);
                foreach (var path in GeometryPaths(islandGeometry, -contourOffset, 0))
                    clips.Add(path);
            }

            if (clips.Count == 0)
                return null;

            var intersection = Clipper.Intersect(subject, clips, FillRule.NonZero, Precision);
            if (!intersection.Any(path => Math.Abs(Clipper.Area(path)) > GeometryTolerances.Degenerate))
                return null;

            var difference = Clipper.Difference(subject, clips, FillRule.NonZero, Precision);
            return new PocketRegionGeometry(difference);
        }

        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
        {
            var tree = Tree(toolRadius + taperOffset);
            var result = new List<IPocketGeometry>(tree.Count);
            for (var index = 0; index < tree.Count; index++)
            {
                var child = tree[index];
                if (child == null || child.Polygon == null || child.Polygon.Count < 3)
                    continue;

                var paths = new PathsD();
                AddNodePaths(child, paths);
                result.Add(new PocketRegionGeometry(paths));
            }
            return result;
        }

        public (double x, double y) GetCenter()
        {
            var outer = LargestOuterPath(Tree(0));
            if (outer == null || outer.Count == 0)
                return (0, 0);

            return Geometry2D.Centroid(ToPoints(outer), GeometryTolerances.Vertex);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            var outer = LargestOuterPath(Tree(toolRadius + taperOffset));
            return outer == null ? EmptyContour.Instance : new PathContour(outer);
        }

        public IReadOnlyList<IContour> GetContours(double toolRadius, double taperOffset)
        {
            var result = new List<IContour>();
            var tree = Tree(toolRadius + taperOffset);
            for (var index = 0; index < tree.Count; index++)
                AddNodeContours(tree[index], result);
            return result;
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            var paths = Flatten(Tree(toolRadius + taperOffset));
            var inside = false;
            foreach (var path in paths)
            {
                var result = Clipper.PointInPolygon(new PointD(x, y), path, Precision);
                if (result == PointInPolygonResult.IsOn)
                    return true;
                if (result == PointInPolygonResult.IsInside)
                    inside = !inside;
            }
            return inside;
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
            => Tree(toolRadius + taperOffset).Count == 0;

        private PolyTreeD Tree(double inwardOffset)
        {
            if (_hasCachedTree && _cachedOffset.Equals(inwardOffset) && _cachedTree != null)
                return _cachedTree;

            PathsD paths;
            if (Math.Abs(inwardOffset) <= GeometryTolerances.Degenerate)
            {
                paths = new PathsD(_paths);
            }
            else
            {
                paths = Clipper.InflatePaths(
                    _paths,
                    -inwardOffset,
                    JoinType.Miter,
                    EndType.Polygon,
                    MiterLimit,
                    Precision);
            }

            var tree = new PolyTreeD();
            Clipper.BooleanOp(
                ClipType.Union,
                paths,
                new PathsD(),
                tree,
                FillRule.NonZero,
                Precision);

            _cachedOffset = inwardOffset;
            _cachedTree = tree;
            _hasCachedTree = true;
            return tree;
        }

        private static PathsD GeometryPaths(
            IPocketGeometry geometry,
            double toolRadius,
            double taperOffset)
        {
            var result = new PathsD();
            if (geometry.SplitsIntoAreas)
            {
                foreach (var area in geometry.GetAreas(toolRadius, taperOffset))
                    AddContourPath(result, area.GetContour(0, 0));
            }
            else if (!geometry.IsContourTooSmall(toolRadius, taperOffset))
            {
                AddContourPath(result, geometry.GetContour(toolRadius, taperOffset));
            }
            return result;
        }

        private static void AddContourPath(PathsD paths, IContour? contour)
        {
            if (contour == null)
                return;

            var path = new PathD();
            foreach (var point in contour.GetPoints())
            {
                if (path.Count > 0
                    && Math.Abs(path[path.Count - 1].x - point.x) <= GeometryTolerances.Vertex
                    && Math.Abs(path[path.Count - 1].y - point.y) <= GeometryTolerances.Vertex)
                {
                    continue;
                }
                path.Add(new PointD(point.x, point.y));
            }

            if (path.Count > 1
                && Math.Abs(path[0].x - path[path.Count - 1].x) <= GeometryTolerances.Vertex
                && Math.Abs(path[0].y - path[path.Count - 1].y) <= GeometryTolerances.Vertex)
            {
                path.RemoveAt(path.Count - 1);
            }

            if (path.Count < 3 || Math.Abs(Clipper.Area(path)) <= GeometryTolerances.Degenerate)
                return;
            if (!Clipper.IsPositive(path))
                path.Reverse();
            paths.Add(path);
        }

        private static void AddNodePaths(PolyPathD node, PathsD paths)
        {
            if (node.Polygon != null && node.Polygon.Count >= 3)
            {
                var path = new PathD(node.Polygon);
                var shouldBePositive = !node.IsHole;
                if (Clipper.IsPositive(path) != shouldBePositive)
                    path.Reverse();
                paths.Add(path);
            }

            for (var index = 0; index < node.Count; index++)
                AddNodePaths(node[index], paths);
        }

        private static void AddNodeContours(PolyPathD node, List<IContour> contours)
        {
            if (node?.Polygon != null && node.Polygon.Count >= 3)
                contours.Add(new PathContour(node.Polygon));
            if (node == null)
                return;
            for (var index = 0; index < node.Count; index++)
                AddNodeContours(node[index], contours);
        }

        private static PathsD Flatten(PolyTreeD tree)
        {
            var result = new PathsD();
            for (var index = 0; index < tree.Count; index++)
                AddNodePaths(tree[index], result);
            return result;
        }

        private static PathD? LargestOuterPath(PolyTreeD tree)
        {
            PathD? largest = null;
            var largestArea = 0.0;
            for (var index = 0; index < tree.Count; index++)
            {
                var path = tree[index].Polygon;
                if (path == null)
                    continue;
                var area = Math.Abs(Clipper.Area(path));
                if (area > largestArea)
                {
                    largest = path;
                    largestArea = area;
                }
            }
            return largest;
        }

        private static List<Point2D> ToPoints(PathD path)
        {
            var points = new List<Point2D>(path.Count);
            foreach (var point in path)
                points.Add(new Point2D { X = point.x, Y = point.y });
            return points;
        }

        private sealed class PathContour : IContour
        {
            private readonly PathD _path;

            public PathContour(PathD path)
            {
                _path = path;
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                foreach (var point in _path)
                    yield return (point.x, point.y);
                if (_path.Count > 0)
                    yield return (_path[0].x, _path[0].y);
            }

            public double GetArea() => Math.Abs(Clipper.Area(_path));
        }

        private sealed class EmptyContour : IContour
        {
            public static readonly EmptyContour Instance = new EmptyContour();

            public IEnumerable<(double x, double y)> GetPoints()
            {
                yield break;
            }

            public double GetArea() => 0;
        }
    }
}
