#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Восстанавливает замкнутые области из DXF-полилиний, соединяя сегменты
    /// и учитывая точки их пересечения.
    /// </summary>
    internal sealed class DxfClosedContourBuilder
    {
        private const double ClosedContourTolerance = GeometryTolerances.PointCoincidence;
        private readonly DxfSegmentConnector _segmentConnector =
            new DxfSegmentConnector(ClosedContourTolerance);
        private readonly DxfSegmentIntersectionSplitter _intersectionSplitter =
            new DxfSegmentIntersectionSplitter(ClosedContourTolerance);
        private readonly DxfPointCycleFinder _pointCycleFinder =
            new DxfPointCycleFinder(ClosedContourTolerance);

        internal List<Polyline2D> Build(List<Polyline2D> allPolylines, CancellationToken cancellation = default)
        {
            // Теперь пытаемся соединить отдельные линии и дуги в замкнутые контуры
            var connectedContours = _segmentConnector.Connect(allPolylines);

            // Ищем замкнутые области, образованные пересекающимися линиями
            var intersectionContours = FindClosedAreasFromIntersections(allPolylines, cancellation);
            
            var closedContours = new List<Polyline2D>();
            AddUniqueClosedContours(closedContours, allPolylines);
            AddUniqueClosedContours(closedContours, connectedContours);
            AddUniqueClosedContours(closedContours, intersectionContours);

            return closedContours;
        }

        private void AddUniqueClosedContours(
            List<Polyline2D> destination,
            IEnumerable<Polyline2D> candidates)
        {
            foreach (var contour in candidates)
            {
                if (IsClosedContour(contour)
                    && !destination.Any(existing => AreContoursSimilar(contour, existing)))
                    destination.Add(contour);
            }
        }

        private static bool PointsMatch(Point2D p1, Point2D p2)
            => Geometry2D.PointsMatch(p1, p2, ClosedContourTolerance);

        private List<Polyline2D> FindClosedAreasFromIntersections(
            List<Polyline2D> segments, CancellationToken cancellation)
        {
            var contours = new List<Polyline2D>();

            if (segments == null || segments.Count == 0)
                return contours;

            // Находим все точки пересечения и разбиваем сегменты
            var splitSegments = _intersectionSplitter.Split(segments);

            if (splitSegments == null || splitSegments.Count == 0)
                return contours;

            var cycleContours = _pointCycleFinder.FindContours(splitSegments, cancellation);

            // Фильтруем циклы - оставляем только те, которые образуют
            // невырожденные замкнутые области.
            foreach (var contour in cycleContours)
            {
                if (IsClosedContour(contour) &&
                    GetContourArea(contour) > ClosedContourTolerance * ClosedContourTolerance)
                {
                    contours.Add(contour);
                }
            }

            return contours;
        }
        
        private double GetContourArea(Polyline2D contour)
            => Geometry2D.Area(contour?.Points);

        private bool AreContoursSimilar(Polyline2D c1, Polyline2D c2)
        {
            if (c1?.Points == null || c2?.Points == null)
                return false;
            
            if (Math.Abs(c1.Points.Count - c2.Points.Count) > 2)
                return false;
            
            // Проверяем, совпадают ли точки контуров (с учетом возможного сдвига начала)
            for (int offset = 0; offset < c1.Points.Count; offset++)
            {
                int matchCount = 0;
                for (int i = 0; i < c1.Points.Count && i < c2.Points.Count; i++)
                {
                    int idx1 = (i + offset) % c1.Points.Count;
                    int idx2 = i % c2.Points.Count;
                    if (PointsMatch(c1.Points[idx1], c2.Points[idx2]))
                        matchCount++;
                }
                if (matchCount >= Math.Min(c1.Points.Count, c2.Points.Count) - 1)
                    return true;
            }
            
            return false;
        }

        private bool IsClosedContour(Polyline2D polyline)
        {
            if (polyline?.Points == null || polyline.Points.Count < 3)
                return false;

            var first = polyline.Points[0];
            var last = polyline.Points[polyline.Points.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

    }
}
