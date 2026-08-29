#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
    /// Кривые дискретизируются по максимальному отклонению хорды в реальных
    /// миллиметрах, поэтому крупная окружность не становится грубым
    /// 32-угольником, а масштаб единиц учитывается до аппроксимации.
    /// </summary>
    internal static class DxfEntityReader
    {
        /// <summary>Наименьшее число сегментов аппроксимации окружности.</summary>
        private const int MinimumCircleSegments = 32;

        /// <summary>Наименьшее число сегментов аппроксимации дуги.</summary>
        private const int MinimumArcSegments = 8;

        /// <summary>Наименьшее число сегментов аппроксимации эллипса.</summary>
        private const int MinimumEllipseSegments = 16;

        /// <summary>Допустимое отклонение аналитической кривой от хорды, мм.</summary>
        internal const double MaximumChordDeviationMillimeters = 0.025;

        /// <summary>Защитный предел тесселяции одной аналитической сущности.</summary>
        private const int MaximumCurveSegments = 4096;

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
        /// <param name="cancellation">Отмена разбора и перечисления сущностей.</param>
        /// <exception cref="InvalidDataException">Файл не является DXF-документом.</exception>
        internal static List<Polyline2D> Read(string path, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > GenerationLimits.MaxDxfFileBytes)
            {
                throw new CoreException(
                    CoreErrorCodes.DxfFileTooLarge,
                    "The DXF file exceeds the safe size limit of {0} MB.",
                    GenerationLimits.MaxDxfFileBytes / (1024 * 1024));
            }

            var document = DxfDocument.Load(stream);
            cancellation.ThrowIfCancellationRequested();
            if (document == null)
                throw new CoreException(CoreErrorCodes.DxfNotADrawing,
                    "The file is not a DXF drawing: {0}.", path);

            double scale = GetMillimeterScale(document.DrawingVariables.InsUnits);

            var result = new List<Polyline2D>();
            var budget = new ImportBudget();
            foreach (var entity in document.Entities.All)
            {
                cancellation.ThrowIfCancellationRequested();
                AppendEntity(entity, scale, result, budget, 0, cancellation);
            }

            return result;
        }

        /// <summary>
        /// Коэффициент перевода координат чертежа в миллиметры. Безразмерный
        /// DXF неоднозначен: одно и то же значение может означать миллиметры,
        /// дюймы или другую единицу, поэтому такой файл нельзя безопасно
        /// превращать в траекторию станка.
        /// </summary>
        private static double GetMillimeterScale(DrawingUnits units)
        {
            if (units == DrawingUnits.Unitless ||
                !Enum.IsDefined(typeof(DrawingUnits), units))
            {
                throw new CoreException(CoreErrorCodes.DxfUnitsNotSpecified,
                    "The DXF drawing has no supported linear units. Set INSUNITS in the source drawing before importing it.");
            }

            var scale = UnitHelper.ConversionFactor(units, DrawingUnits.Millimeters);
            if (!double.IsFinite(scale) || scale <= 0)
            {
                throw new CoreException(CoreErrorCodes.DxfUnitsNotSpecified,
                    "The DXF drawing has no supported linear units. Set INSUNITS in the source drawing before importing it.");
            }

            return scale;
        }

        private static void AppendEntity(
            EntityObject entity,
            double scale,
            List<Polyline2D> result,
            ImportBudget budget,
            int insertDepth,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            budget.ObserveEntity(insertDepth);
            switch (entity)
            {
                case Line line:
                    Add(result, budget, new[]
                    {
                        Point(line.StartPoint.X, line.StartPoint.Y, scale),
                        Point(line.EndPoint.X, line.EndPoint.Y, scale)
                    });
                    break;

                case Circle circle:
                    Add(result, budget, ApproximateCircle(circle, scale));
                    break;

                case Arc arc:
                    Add(result, budget, ApproximateArc(arc, scale));
                    break;

                case Ellipse ellipse:
                    Add(result, budget, ApproximateEllipse(ellipse, scale));
                    break;

                case DrawingPolyline polyline2D:
                    Add(result, budget, ReadPolyline(polyline2D, scale, budget.RemainingPoints));
                    break;

                case Polyline3D polyline3D:
                    Add(result, budget, ReadPolyline3D(polyline3D, scale, budget.RemainingPoints));
                    break;

                case Spline spline:
                    Add(result, budget, ApproximateSpline(spline, scale));
                    break;

                case Insert insert:
                    // Вставка блока: раскрываем в сущности с координатами модели.
                    foreach (var exploded in insert.Explode())
                    {
                        cancellation.ThrowIfCancellationRequested();
                        AppendEntity(exploded, scale, result, budget, insertDepth + 1, cancellation);
                    }
                    break;

                default:
                    // Тексты, размеры, штриховки и прочее геометрией контура не являются.
                    break;
            }
        }

        private static void Add(
            List<Polyline2D> result,
            ImportBudget budget,
            IReadOnlyList<Point2D>? points)
        {
            // null — сущность выродилась (нулевой радиус, слишком мало
            // вершин) и контура не даёт.
            if (points == null || points.Count < 2)
                return;
            budget.AddContour(points.Count);
            result.Add(new Polyline2D { Points = new List<Point2D>(points) });
        }

        private static Point2D Point(double x, double y, double scale)
            => new Point2D { X = x * scale, Y = y * scale };

        private static List<Point2D>? ApproximateCircle(Circle circle, double scale)
        {
            if (circle.Radius <= 0)
                return null;

            var segments = CurveSegmentCount(circle.Radius * scale, 2.0 * Math.PI,
                MinimumCircleSegments);
            var points = new List<Point2D>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                var angle = 2.0 * Math.PI * i / segments;
                points.Add(Point(
                    circle.Center.X + circle.Radius * Math.Cos(angle),
                    circle.Center.Y + circle.Radius * Math.Sin(angle),
                    scale));
            }
            return points;
        }

        private static List<Point2D>? ApproximateArc(Arc arc, double scale)
        {
            if (arc.Radius <= 0)
                return null;

            var startAngle = arc.StartAngle * Math.PI / 180.0;
            var endAngle = arc.EndAngle * Math.PI / 180.0;
            while (endAngle < startAngle)
                endAngle += 2.0 * Math.PI;

            var angleSpan = endAngle - startAngle;
            var segments = CurveSegmentCount(arc.Radius * scale, angleSpan,
                MinimumArcSegments);

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

        private static List<Point2D>? ApproximateEllipse(Ellipse ellipse, double scale)
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
            // MajorRadius даёт консервативную верхнюю оценку отклонения
            // параметрической хорды для обеих осей эллипса.
            var segments = CurveSegmentCount(majorRadius * scale, paramSpan,
                MinimumEllipseSegments);

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

        private static int CurveSegmentCount(double radiusMillimeters, double angleSpan,
            int minimumSegments)
        {
            if (!double.IsFinite(radiusMillimeters) || radiusMillimeters <= 0 ||
                !double.IsFinite(angleSpan) || angleSpan <= 0)
                return minimumSegments;

            // sagitta = r * (1 - cos(theta / 2)); отсюда максимально
            // допустимый центральный угол одной хорды.
            var normalizedDeviation = Math.Min(1.0,
                MaximumChordDeviationMillimeters / radiusMillimeters);
            var maximumAngle = 2.0 * Math.Acos(1.0 - normalizedDeviation);
            if (!double.IsFinite(maximumAngle) || maximumAngle <= 0)
                return MaximumCurveSegments;

            var required = (int)Math.Ceiling(angleSpan / maximumAngle);
            return Math.Min(MaximumCurveSegments, Math.Max(minimumSegments, required));
        }

        /// <summary>
        /// Полилиния чертежа. Она раскрывается на отрезки и дуги, а дуги
        /// разбиваются той же формулой, что и отдельная сущность ARC, — иначе
        /// скругление внутри полилинии описывалось бы грубее, чем такая же
        /// дуга, нарисованная отдельно. Замкнутая полилиния возвращается
        /// с повторением первой вершины в конце, как её и ожидает сборка
        /// контуров.
        /// </summary>
        private static List<Point2D>? ReadPolyline(
            DrawingPolyline polyline,
            double scale,
            int maximumPoints)
        {
            if (polyline.Vertexes.Count < 2)
                return null;

            var points = new List<Point2D>();
            foreach (var segment in polyline.Explode())
            {
                List<Point2D>? segmentPoints;
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
                {
                    if (points.Count >= maximumPoints)
                        throw TooComplex();
                    points.Add(segmentPoints[i]);
                }
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
        private static List<Point2D>? ApproximateSpline(Spline spline, double scale)
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

        private static List<Point2D>? ReadPolyline3D(
            Polyline3D polyline,
            double scale,
            int maximumPoints)
        {
            if (polyline.Vertexes.Count < 2)
                return null;
            var requiredPoints = polyline.Vertexes.Count + (polyline.IsClosed ? 1 : 0);
            if (requiredPoints > maximumPoints)
                throw TooComplex();

            var points = new List<Point2D>(polyline.Vertexes.Count + 1);
            foreach (var vertex in polyline.Vertexes)
                points.Add(Point(vertex.X, vertex.Y, scale));

            if (polyline.IsClosed)
                points.Add(Point(polyline.Vertexes[0].X, polyline.Vertexes[0].Y, scale));

            return points;
        }

        private static CoreException TooComplex()
            => new CoreException(
                CoreErrorCodes.DxfTooComplex,
                "The DXF drawing exceeds the safe entity, nesting, contour or point limits.");

        private sealed class ImportBudget
        {
            private int _entities;
            private int _contours;
            private int _points;

            internal int RemainingPoints => GenerationLimits.MaxImportedPointsPerOperation - _points;

            internal void ObserveEntity(int insertDepth)
            {
                _entities++;
                if (_entities > GenerationLimits.MaxImportedEntities
                    || insertDepth > GenerationLimits.MaxDxfInsertDepth)
                    throw TooComplex();
            }

            internal void AddContour(int pointCount)
            {
                _contours++;
                if (_contours > GenerationLimits.MaxImportedContoursPerOperation)
                    throw TooComplex();
                if (pointCount > RemainingPoints)
                    throw TooComplex();
                _points += pointCount;
                if (_points > GenerationLimits.MaxImportedPointsPerOperation)
                    throw TooComplex();
            }
        }
    }
}
