using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Восстанавливает замкнутые области из DXF-полилиний, соединяя сегменты
    /// и учитывая точки их пересечения.
    /// </summary>
    internal sealed class DxfClosedContourBuilder
    {
        private const double ClosedContourTolerance = 0.001;
        private readonly DxfSegmentConnector _segmentConnector =
            new DxfSegmentConnector(ClosedContourTolerance);
        private readonly DxfSegmentIntersectionSplitter _intersectionSplitter =
            new DxfSegmentIntersectionSplitter(ClosedContourTolerance);
        private readonly DxfPointCycleFinder _pointCycleFinder =
            new DxfPointCycleFinder(ClosedContourTolerance);

        internal List<DxfPolyline> Build(List<DxfPolyline> allPolylines)
        {
            // Теперь пытаемся соединить отдельные линии и дуги в замкнутые контуры
            var connectedContours = _segmentConnector.Connect(allPolylines);
            
            // Ищем замкнутые области, образованные пересекающимися линиями
            var intersectionContours = FindClosedAreasFromIntersections(allPolylines);
            
            var closedContours = new List<DxfPolyline>();
            AddUniqueClosedContours(closedContours, allPolylines);
            AddUniqueClosedContours(closedContours, connectedContours);
            AddUniqueClosedContours(closedContours, intersectionContours);

            return closedContours;
        }

        private void AddUniqueClosedContours(
            List<DxfPolyline> destination,
            IEnumerable<DxfPolyline> candidates)
        {
            foreach (var contour in candidates)
            {
                if (IsClosedContour(contour)
                    && !destination.Any(existing => AreContoursSimilar(contour, existing)))
                    destination.Add(contour);
            }
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= ClosedContourTolerance;
        }

        private List<DxfPolyline> FindClosedAreasFromIntersections(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            
            if (segments == null || segments.Count == 0)
                return contours;
            
            // Находим все точки пересечения и разбиваем сегменты
            var splitSegments = _intersectionSplitter.Split(segments);
            
            if (splitSegments == null || splitSegments.Count == 0)
                return contours;
            
            var cycleContours = _pointCycleFinder.FindContours(splitSegments);

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
        
        private double GetContourArea(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 3)
                return 0;
            
            double area = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(area / 2.0);
        }

        private bool AreContoursSimilar(DxfPolyline c1, DxfPolyline c2)
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

        private bool IsClosedContour(DxfPolyline polyline)
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
