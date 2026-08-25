using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.Preview
{
    /// <summary>
    /// Builds an <see cref="OperationScene"/> from operations (plan item 6.3).
    /// Contour point generation moved here from the OperationsPreviewView
    /// code-behind:
    /// - profiles: <see cref="ProfileGeometryFactory"/> (on-line contour,
    ///   toolOffset 0 — the preview shows the contour, not the tool path);
    /// - pockets: <see cref="PocketGeometryFactory"/> (contour with toolRadius
    ///   0, taperOffset 0);
    /// - DXF: raw polylines/contours from the operation (the geometry
    ///   factories merge or offset them, which does not fit the preview);
    /// - drilling: one point per hole.
    /// </summary>
    public static class OperationSceneBuilder
    {
        public static OperationScene Build(IEnumerable<OperationBase> operations)
        {
            var shapes = new List<OperationShape>();
            if (operations == null)
                return new OperationScene(shapes);

            foreach (var operation in operations)
            {
                if (operation == null)
                    continue;

                if (operation is DrillPointsOperation drill)
                {
                    foreach (var hole in drill.Holes)
                        shapes.Add(new OperationShape(operation, OperationShapeKind.Point,
                            new[] { (hole.X, hole.Y) }, false, false));
                }
                else if (operation is ProfileDxfOperation dxfProfile)
                {
                    foreach (var polyline in dxfProfile.Polylines ?? new List<DxfPolyline>())
                    {
                        if (polyline?.Points == null || polyline.Points.Count < 2)
                            continue;

                        var points = new List<(double X, double Y)>(polyline.Points.Count);
                        foreach (var p in polyline.Points)
                            points.Add((p.X, p.Y));

                        shapes.Add(new OperationShape(operation, OperationShapeKind.Contour,
                            points, IsClosed(points), false));
                    }
                }
                else if (operation is IProfileOperation profile)
                {
                    var geometry = ProfileGeometryFactory.Create(operation);
                    var points = new List<(double X, double Y)>();
                    foreach (var (x, y) in geometry.GetContourPoints(0, profile.Direction))
                        points.Add((x, y));

                    if (points.Count > 0)
                        shapes.Add(new OperationShape(operation, OperationShapeKind.Contour,
                            points, IsClosed(points), false));
                }
                else if (operation is PocketDxfOperation dxfPocket)
                {
                    foreach (var contour in dxfPocket.ClosedContours ?? new List<DxfPolyline>())
                    {
                        if (contour?.Points == null || contour.Points.Count < 3)
                            continue;

                        var points = new List<(double X, double Y)>(contour.Points.Count);
                        foreach (var p in contour.Points)
                            points.Add((p.X, p.Y));

                        shapes.Add(new OperationShape(operation, OperationShapeKind.Contour,
                            points, IsClosed(points), true));
                    }
                }
                else if (operation is IPocketOperation)
                {
                    var geometry = PocketGeometryFactory.Create(operation);
                    var points = new List<(double X, double Y)>();
                    foreach (var (x, y) in geometry.GetContour(0, 0).GetPoints())
                        points.Add((x, y));

                    if (points.Count >= 3)
                        shapes.Add(new OperationShape(operation, OperationShapeKind.Contour,
                            points, IsClosed(points), true));
                }
            }

            return new OperationScene(shapes);
        }

        private static bool IsClosed(IReadOnlyList<(double X, double Y)> points)
        {
            if (points.Count < 2)
                return false;

            var first = points[0];
            var last = points[points.Count - 1];
            return Math.Abs(first.X - last.X) < GeometryTolerances.Vertex
                && Math.Abs(first.Y - last.Y) < GeometryTolerances.Vertex;
        }
    }
}
