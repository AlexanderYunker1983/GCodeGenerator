#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Geometry
{
    /// <summary>
    /// Реализация геометрии для эллиптического кармана.
    ///
    /// Эквидистанта стенки считается честно — смещением контура через
    /// <see cref="ContourOffset"/>. Прежняя реализация вычитала смещение
    /// из полуосей, но уменьшенный эллипс — не эквидистанта эллипса: точка
    /// сдвигается радиально, а не по нормали, и в средних участках
    /// квадрантов траектория не доходила до стенки — карман получался уже
    /// задуманного.
    /// </summary>
    public class EllipsePocketGeometry : IPocketGeometry
    {
        /// <summary>Одна фигура — одна область: перемычкам взяться неоткуда.</summary>
        public bool SplitsIntoAreas => false;

        /// <inheritdoc />
        public IReadOnlyList<IPocketGeometry> GetAreas(double toolRadius, double taperOffset)
            => System.Array.Empty<IPocketGeometry>();

        private readonly PocketEllipseOperation _operation;

        // Кеш последней эквидистанты, по образцу DxfPocketGeometry: в пределах
        // слоя смещение одинаково для всех вызовов (контур, принадлежность
        // каждой точки траектории спирали), а меняется только между слоями.
        private bool _hasCachedOffset;
        private double _cachedOffsetDelta;
        private List<Point2D> _cachedOffsetContour = new List<Point2D>();

        public EllipsePocketGeometry(PocketEllipseOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        public (double x, double y) GetCenter()
        {
            return (_operation.CenterX, _operation.CenterY);
        }

        public IContour GetContour(double toolRadius, double taperOffset)
        {
            return new PolylineContour(OffsetContour(toolRadius + taperOffset));
        }

        public bool IsPointInside(double x, double y, double toolRadius, double taperOffset)
        {
            var contour = OffsetContour(toolRadius + taperOffset);
            return contour.Count >= 3 && Geometry2D.IsPointInsidePolygon(x, y, contour);
        }

        public bool IsContourTooSmall(double toolRadius, double taperOffset)
        {
            double effectiveToolRadius = toolRadius + taperOffset;
            double toolDiameter = toolRadius * 2.0;
            
            // Минимальный порог размера контура: 5% от диаметра фрезы
            double minSizeThreshold = toolDiameter * 0.05;
            
            // Вычисляем эффективные диаметры (уже с учетом фрезы и уклона)
            double effectiveDiameterX = (_operation.RadiusX - effectiveToolRadius) * 2.0;
            double effectiveDiameterY = (_operation.RadiusY - effectiveToolRadius) * 2.0;
            
            // Контур слишком маленький, если любой из диаметров меньше минимального порога
            return effectiveDiameterX < minSizeThreshold - GeometryTolerances.Vertex
                || effectiveDiameterY < minSizeThreshold - GeometryTolerances.Vertex;
        }

        /// <summary>
        /// Эквидистанта стенки: контур эллипса, смещённый внутрь на
        /// <paramref name="inwardDelta"/>. Пустой список означает, что фреза
        /// с таким смещением в карман не помещается — этот случай отсекает
        /// <see cref="IsContourTooSmall"/> до построения слоя, поэтому
        /// «разумное значение» здесь не подставляется.
        /// </summary>
        private List<Point2D> OffsetContour(double inwardDelta)
        {
            if (_hasCachedOffset && _cachedOffsetDelta.Equals(inwardDelta))
                return _cachedOffsetContour;

            var parts = ContourOffset.Offset(TessellateBase(), -inwardDelta);
            var largest = new List<Point2D>();
            double largestArea = 0.0;
            foreach (var part in parts)
            {
                var area = Geometry2D.Area(part);
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = part;
                }
            }

            _cachedOffsetContour = largest;
            _cachedOffsetDelta = inwardDelta;
            _hasCachedOffset = true;
            return largest;
        }

        /// <summary>
        /// Тесселяция самого эллипса — вход для смещения. Плотность прежняя:
        /// шаг полмиллиметра, не меньше 32 сегментов.
        /// </summary>
        private List<Point2D> TessellateBase()
        {
            double h = Math.Pow(_operation.RadiusX - _operation.RadiusY, 2)
                / Math.Pow(_operation.RadiusX + _operation.RadiusY, 2);
            double perimeter = Math.PI * (_operation.RadiusX + _operation.RadiusY)
                * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            int segments = Math.Max(32, (int)Math.Ceiling(perimeter / 0.5));

            double rotationRad = _operation.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotationRad);
            double sinRot = Math.Sin(rotationRad);

            var points = new List<Point2D>(segments);
            for (int i = 0; i < segments; i++)
            {
                double t = 2 * Math.PI * i / segments;
                double xEllipse = _operation.RadiusX * Math.Cos(t);
                double yEllipse = _operation.RadiusY * Math.Sin(t);
                points.Add(new Point2D
                {
                    X = _operation.CenterX + xEllipse * cosRot - yEllipse * sinRot,
                    Y = _operation.CenterY + xEllipse * sinRot + yEllipse * cosRot,
                });
            }

            return points;
        }

        /// <summary>
        /// Контур-ломаная: точки эквидистанты с замыканием, площадь — по ним же.
        /// </summary>
        private class PolylineContour : IContour
        {
            private readonly List<Point2D> _points;

            public PolylineContour(List<Point2D> points)
            {
                _points = points;
            }

            public IEnumerable<(double x, double y)> GetPoints()
            {
                if (_points.Count == 0)
                    yield break;

                foreach (var point in _points)
                    yield return (point.X, point.Y);

                // Замыкаем контур: смещение возвращает вершины без дубликата.
                yield return (_points[0].X, _points[0].Y);
            }

            public double GetArea()
            {
                return Geometry2D.Area(_points);
            }
        }
    }
}

