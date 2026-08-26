using System;
using System.Collections.Generic;
using System.IO;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using netDxf;
using netDxf.Entities;
using netDxf.Units;
// В netDxf есть собственный Polyline2D — сущность чертежа. Наша ломаная
// описывает уже разобранную геометрию, поэтому имена разводятся явно.
using DrawingPolyline = netDxf.Entities.Polyline2D;
using Polyline2D = GCodeGenerator.Models.Polyline2D;

namespace GCodeGenerator.Import
{
    /// <summary>
    /// Разбирает DXF-файл в независимые полилинии.
    ///
    /// Разбор формата выполняет netDxf. Прежний построчный разбор читал файл
    /// целиком как массив строк и знал только шесть типов сущностей вне всякой
    /// структуры файла: он игнорировал секции (сущности искались и в шапке,
    /// и в таблицах, и в блоках), не раскрывал вставки блоков, не читал
    /// единицы чертежа и терял дуги внутри полилиний — параметр bulge
    /// не разбирался, и дуга молча превращалась в хорду. Неразобранное число
    /// превращалось в координату 0, то есть битый файл давал не ошибку,
    /// а тихо испорченную геометрию.
    ///
    /// Дискретизация окружностей, дуг и эллипсов осталась прежней: сегменты
    /// считаются теми же формулами, поэтому геометрия ранее импортированных
    /// чертежей не меняется.
    /// </summary>
    internal static class DxfEntityReader
    {
        /// <summary>Число сегментов аппроксимации окружности.</summary>
        private const int CircleSegments = 32;

        /// <summary>Наименьшее число сегментов аппроксимации дуги.</summary>
        private const int MinimumArcSegments = 8;

        /// <summary>Наименьшее число сегментов аппроксимации эллипса.</summary>
        private const int MinimumEllipseSegments = 16;

        /// <summary>Число сегментов пробной оценки длины сплайна.</summary>
        private const int SplineProbeSegments = 16;

        /// <summary>Хорда аппроксимации сплайна, мм.</summary>
        private const double SplineChordMillimeters = 0.5;

        /// <summary>Верхний предел числа сегментов сплайна.</summary>
        private const int MaximumSplineSegments = 512;

        /// <summary>
        /// Читает геометрию чертежа. Координаты приводятся к миллиметрам
        /// по единицам чертежа.
        /// </summary>
        /// <param name="path">Путь к DXF-файлу.</param>
        /// <exception cref="InvalidDataException">Файл не является DXF-документом.</exception>
        internal static List<Polyline2D> Read(string path)
        {
            var document = DxfDocument.Load(path);
            if (document == null)
                throw new InvalidDataException($"Файл не является DXF-документом: {path}");

            double scale = GetMillimeterScale(document.DrawingVariables.InsUnits);

            var result = new List<Polyline2D>();
            foreach (var entity in document.Entities.All)
                AppendEntity(entity, scale, result);

            return result;
        }

        /// <summary>
        /// Коэффициент перевода координат чертежа в миллиметры. Чертёж без
        /// заданных единиц трактуется как миллиметровый: так же работал
        /// прежний разбор.
        /// </summary>
        private static double GetMillimeterScale(DrawingUnits units)
        {
            switch (units)
            {
                case DrawingUnits.Centimeters: return 10.0;
                case DrawingUnits.Decimeters: return 100.0;
                case DrawingUnits.Meters: return 1000.0;
                case DrawingUnits.Inches: return 25.4;
                case DrawingUnits.Feet: return 304.8;
                case DrawingUnits.Yards: return 914.4;
                case DrawingUnits.Microinches: return 25.4e-6;
                case DrawingUnits.Mils: return 0.0254;
                case DrawingUnits.Microns: return 0.001;
                case DrawingUnits.Millimeters:
                case DrawingUnits.Unitless:
                default:
                    return 1.0;
            }
        }

        private static void AppendEntity(EntityObject entity, double scale, List<Polyline2D> result)
        {
            switch (entity)
            {
                case Line line:
                    Add(result, new[]
                    {
                        Point(line.StartPoint.X, line.StartPoint.Y, scale),
                        Point(line.EndPoint.X, line.EndPoint.Y, scale)
                    });
                    break;

                case Circle circle:
                    Add(result, ApproximateCircle(circle, scale));
                    break;

                case Arc arc:
                    Add(result, ApproximateArc(arc, scale));
                    break;

                case Ellipse ellipse:
                    Add(result, ApproximateEllipse(ellipse, scale));
                    break;

                case DrawingPolyline polyline2D:
                    Add(result, ReadPolyline(polyline2D, scale));
                    break;

                case Polyline3D polyline3D:
                    Add(result, ReadPolyline3D(polyline3D, scale));
                    break;

                case Spline spline:
                    Add(result, ApproximateSpline(spline, scale));
                    break;

                case Insert insert:
                    // Вставка блока: раскрываем в сущности с координатами модели.
                    foreach (var exploded in insert.Explode())
                        AppendEntity(exploded, scale, result);
                    break;

                default:
                    // Тексты, размеры, штриховки и прочее геометрией контура не являются.
                    break;
            }
        }

        private static void Add(List<Polyline2D> result, IReadOnlyList<Point2D> points)
        {
            if (points == null || points.Count < 2)
                return;
            result.Add(new Polyline2D { Points = new List<Point2D>(points) });
        }

        private static Point2D Point(double x, double y, double scale)
            => new Point2D { X = x * scale, Y = y * scale };

        private static List<Point2D> ApproximateCircle(Circle circle, double scale)
        {
            if (circle.Radius <= 0)
                return null;

            var points = new List<Point2D>(CircleSegments + 1);
            for (int i = 0; i <= CircleSegments; i++)
            {
                var angle = 2.0 * Math.PI * i / CircleSegments;
                points.Add(Point(
                    circle.Center.X + circle.Radius * Math.Cos(angle),
                    circle.Center.Y + circle.Radius * Math.Sin(angle),
                    scale));
            }
            return points;
        }

        private static List<Point2D> ApproximateArc(Arc arc, double scale)
        {
            if (arc.Radius <= 0)
                return null;

            var startAngle = arc.StartAngle * Math.PI / 180.0;
            var endAngle = arc.EndAngle * Math.PI / 180.0;
            while (endAngle < startAngle)
                endAngle += 2.0 * Math.PI;

            var angleSpan = endAngle - startAngle;
            var segments = Math.Max(MinimumArcSegments, (int)(angleSpan / (Math.PI / 16.0)));

            var points = new List<Point2D>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                var angle = startAngle + angleSpan * i / segments;
                points.Add(Point(
                    arc.Center.X + arc.Radius * Math.Cos(angle),
                    arc.Center.Y + arc.Radius * Math.Sin(angle),
                    scale));
            }
            return points;
        }

        private static List<Point2D> ApproximateEllipse(Ellipse ellipse, double scale)
        {
            double majorRadius = ellipse.MajorAxis / 2.0;
            double minorRadius = ellipse.MinorAxis / 2.0;
            if (majorRadius <= 0 || minorRadius <= 0)
                return null;

            var startParam = ellipse.StartAngle * Math.PI / 180.0;
            var endParam = ellipse.EndAngle * Math.PI / 180.0;
            while (endParam <= startParam)
                endParam += 2.0 * Math.PI;

            var paramSpan = endParam - startParam;
            var segments = Math.Max(MinimumEllipseSegments, (int)(paramSpan / (Math.PI / 16.0)));

            var rotation = ellipse.Rotation * Math.PI / 180.0;
            var cosRotation = Math.Cos(rotation);
            var sinRotation = Math.Sin(rotation);

            var points = new List<Point2D>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                var parameter = startParam + paramSpan * i / segments;
                var localX = majorRadius * Math.Cos(parameter);
                var localY = minorRadius * Math.Sin(parameter);
                points.Add(Point(
                    ellipse.Center.X + localX * cosRotation - localY * sinRotation,
                    ellipse.Center.Y + localX * sinRotation + localY * cosRotation,
                    scale));
            }
            return points;
        }

        /// <summary>
        /// Полилиния чертежа. Она раскрывается на отрезки и дуги, а дуги
        /// разбиваются той же формулой, что и отдельная сущность ARC, — иначе
        /// скругление внутри полилинии описывалось бы грубее, чем такая же
        /// дуга, нарисованная отдельно. Замкнутая полилиния возвращается
        /// с повторением первой вершины в конце, как её и ожидает сборка
        /// контуров.
        /// </summary>
        private static List<Point2D> ReadPolyline(DrawingPolyline polyline, double scale)
        {
            if (polyline.Vertexes.Count < 2)
                return null;

            var points = new List<Point2D>();
            foreach (var segment in polyline.Explode())
            {
                List<Point2D> segmentPoints;
                switch (segment)
                {
                    case Line line:
                        segmentPoints = new List<Point2D>
                        {
                            Point(line.StartPoint.X, line.StartPoint.Y, scale),
                            Point(line.EndPoint.X, line.EndPoint.Y, scale)
                        };
                        break;
                    case Arc arc:
                        segmentPoints = ApproximateArc(arc, scale);
                        break;
                    default:
                        continue;
                }

                if (segmentPoints == null || segmentPoints.Count == 0)
                    continue;

                // Конец предыдущего сегмента и начало следующего — одна вершина.
                int startIndex = points.Count > 0
                    && Math.Abs(points[points.Count - 1].X - segmentPoints[0].X) < GeometryTolerances.Vertex
                    && Math.Abs(points[points.Count - 1].Y - segmentPoints[0].Y) < GeometryTolerances.Vertex
                    ? 1
                    : 0;

                for (int i = startIndex; i < segmentPoints.Count; i++)
                    points.Add(segmentPoints[i]);
            }

            return points;
        }

        /// <summary>
        /// Сплайн: обычный результат экспорта из векторных редакторов и CAD.
        /// Прежде сущность молча пропускалась — контур из отрезков и одного
        /// сплайна терял ребро и не замыкался, карман «не находил замкнутых
        /// контуров», хотя netDxf умеет разбивать сплайн на хорды. Угловой
        /// меры, как у дуг, у сплайна нет, поэтому число сегментов
        /// подбирается по длине кривой: пробная ломаная оценивает длину,
        /// шаг хорды — полмиллиметра, та же плотность, с которой продукт
        /// тесселирует собственные фигуры.
        /// </summary>
        private static List<Point2D> ApproximateSpline(Spline spline, double scale)
        {
            var probe = spline.PolygonalVertexes(SplineProbeSegments);
            if (probe == null || probe.Count < 2)
                return null;

            double lengthMillimeters = 0.0;
            for (int i = 1; i < probe.Count; i++)
            {
                var dx = (probe[i].X - probe[i - 1].X) * scale;
                var dy = (probe[i].Y - probe[i - 1].Y) * scale;
                lengthMillimeters += Math.Sqrt(dx * dx + dy * dy);
            }

            var segments = Math.Min(MaximumSplineSegments, Math.Max(SplineProbeSegments,
                (int)Math.Ceiling(lengthMillimeters / SplineChordMillimeters)));

            var vertexes = spline.PolygonalVertexes(segments);
            if (vertexes == null || vertexes.Count < 2)
                return null;

            var points = new List<Point2D>(vertexes.Count + 1);
            foreach (var vertex in vertexes)
                points.Add(Point(vertex.X, vertex.Y, scale));

            // Замкнутый сплайн возвращается с повторением первой вершины,
            // как и замкнутая полилиния: так его ожидает сборка контуров.
            var first = points[0];
            var last = points[points.Count - 1];
            if (spline.IsClosedPeriodic
                && (Math.Abs(first.X - last.X) >= GeometryTolerances.Vertex
                    || Math.Abs(first.Y - last.Y) >= GeometryTolerances.Vertex))
            {
                points.Add(new Point2D { X = first.X, Y = first.Y });
            }

            return points;
        }

        private static List<Point2D> ReadPolyline3D(Polyline3D polyline, double scale)
        {
            if (polyline.Vertexes.Count < 2)
                return null;

            var points = new List<Point2D>(polyline.Vertexes.Count + 1);
            foreach (var vertex in polyline.Vertexes)
                points.Add(Point(vertex.X, vertex.Y, scale));

            if (polyline.IsClosed)
                points.Add(Point(polyline.Vertexes[0].X, polyline.Vertexes[0].Y, scale));

            return points;
        }
    }
}
