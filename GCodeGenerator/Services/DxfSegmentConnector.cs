using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Connects ordered or reversed DXF segments whose endpoints match within
    /// the configured tolerance.
    /// </summary>
    internal sealed class DxfSegmentConnector
    {
        private readonly double _tolerance;

        internal DxfSegmentConnector(double tolerance)
        {
            if (tolerance <= 0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            _tolerance = tolerance;
        }

        internal List<DxfPolyline> Connect(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();
            var used = new bool[segments.Count];
            
            for (int i = 0; i < segments.Count; i++)
            {
                if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                    continue;
                
                // Пытаемся построить контур, начиная с этого сегмента
                var contour = BuildContourFromSegment(segments, i, used);
                if (contour != null && contour.Points != null && contour.Points.Count >= 3)
                {
                    contours.Add(contour);
                }
            }
            
            return contours;
        }

        private DxfPolyline BuildContourFromSegment(List<DxfPolyline> segments, int startIdx, bool[] used)
        {
            var contourPoints = new List<DxfPoint>();
            var startPoint = segments[startIdx].Points[0];
            var currentPoint = segments[startIdx].Points[segments[startIdx].Points.Count - 1];
            
            // Добавляем точки первого сегмента
            foreach (var p in segments[startIdx].Points)
            {
                contourPoints.Add(new DxfPoint { X = p.X, Y = p.Y });
            }
            used[startIdx] = true;
            
            // Ищем следующий сегмент, который начинается там, где заканчивается текущий
            while (true)
            {
                int nextSegmentIdx = -1;
                bool reverseNext = false;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (used[i] || segments[i].Points == null || segments[i].Points.Count < 2)
                        continue;
                    
                    var seg = segments[i];
                    var segStart = seg.Points[0];
                    var segEnd = seg.Points[seg.Points.Count - 1];
                    
                    // Проверяем, совпадает ли начало или конец сегмента с текущей точкой
                    if (PointsMatch(currentPoint, segStart))
                    {
                        nextSegmentIdx = i;
                        reverseNext = false;
                        break;
                    }
                    else if (PointsMatch(currentPoint, segEnd))
                    {
                        nextSegmentIdx = i;
                        reverseNext = true;
                        break;
                    }
                }
                
                if (nextSegmentIdx < 0)
                    break; // Не нашли следующий сегмент
                
                // Добавляем точки следующего сегмента
                var nextSeg = segments[nextSegmentIdx];
                if (reverseNext)
                {
                    // Добавляем точки в обратном порядке
                    for (int j = nextSeg.Points.Count - 2; j >= 0; j--) // Пропускаем последнюю точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[0];
                }
                else
                {
                    // Добавляем точки в прямом порядке
                    for (int j = 1; j < nextSeg.Points.Count; j++) // Пропускаем первую точку (она уже есть)
                    {
                        contourPoints.Add(new DxfPoint { X = nextSeg.Points[j].X, Y = nextSeg.Points[j].Y });
                    }
                    currentPoint = nextSeg.Points[nextSeg.Points.Count - 1];
                }
                
                used[nextSegmentIdx] = true;
                
                // Проверяем, замкнулся ли контур
                if (PointsMatch(currentPoint, startPoint))
                {
                    break; // Контур замкнут
                }
            }
            
            return new DxfPolyline { Points = contourPoints };
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
            => Geometry2D.PointsMatch(p1, p2, _tolerance);
    }
}
