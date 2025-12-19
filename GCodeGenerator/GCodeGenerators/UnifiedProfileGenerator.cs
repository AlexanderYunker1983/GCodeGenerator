using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;
using Geometry = GCodeGenerator.GCodeGenerators.Geometry;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Единый генератор для всех типов профилей.
    /// Использует интерфейсы геометрии и классы-помощники для унификации логики.
    /// </summary>
    public class UnifiedProfileGenerator : IOperationGenerator
    {
        private readonly ProfileGenerationHelper _helper;

        public UnifiedProfileGenerator()
        {
            _helper = new ProfileGenerationHelper();
        }

        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            // Проверяем, что операция является профилем
            if (!(operation is IProfileOperation profileOp))
                return;

            // Создаем геометрию профиля
            var geometry = ProfileGeometryFactory.Create(operation);
            if (geometry == null)
                return;

            // Вычисляем смещение инструмента
            var toolRadius = profileOp.ToolDiameter / 2.0;
            var toolOffset = GCodeGenerationHelper.CalculateToolOffset(profileOp.ToolPathMode, toolRadius);

            // Генерируем цикл по слоям
            _helper.GenerateLayerLoop(
                profileOp,
                (currentZ, nextZ, passNumber) => GenerateLayer(
                    profileOp,
                    geometry,
                    toolOffset,
                    currentZ,
                    nextZ,
                    addLine,
                    g0,
                    g1,
                    settings),
                addLine,
                g0,
                g1,
                settings);
        }

        /// <summary>
        /// Генерирует один слой профиля.
        /// </summary>
        private void GenerateLayer(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            double currentZ,
            double nextZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // Получаем начальную точку контура
            var startPoint = geometry.GetStartPoint(toolOffset);

            // Генерируем вход в материал
            _helper.GenerateEntry(
                op,
                startPoint,
                currentZ,
                nextZ,
                distance => geometry.GetPointOnContour(distance, toolOffset),
                () => geometry.GetPerimeter(toolOffset),
                addLine,
                g0,
                g1,
                settings);

            // После входа мы находимся на начальной точке контура
            // Генерируем путь по контуру
            if (settings.AllowArcs && geometry.SupportsArcs)
            {
                // Используем дуги, если поддерживаются
                var arcSegments = geometry.GetArcSegments(toolOffset).ToList();
                if (arcSegments.Count > 0)
                {
                    GenerateContourWithArcs(op, geometry, toolOffset, arcSegments, addLine, g1, settings);
                }
                else
                {
                    // Fallback на точки, если дуги не доступны
                    GenerateContourFromPoints(op, geometry, toolOffset, startPoint, nextZ, addLine, g0, g1, settings);
                }
            }
            else
            {
                // Генерируем из точек
                GenerateContourFromPoints(op, geometry, toolOffset, startPoint, nextZ, addLine, g0, g1, settings);
            }
        }

        /// <summary>
        /// Генерирует контур из точек.
        /// </summary>
        private void GenerateContourFromPoints(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            (double x, double y) currentPosition,
            double workingZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // Для DXF операций обрабатываем каждую полилинию отдельно
            if (op is ProfileDxfOperation dxfOp)
            {
                GenerateDxfContourFromPoints(dxfOp, geometry, toolOffset, currentPosition, workingZ, addLine, g0, g1, settings);
                return;
            }

            var points = geometry.GetContourPoints(toolOffset, op.Direction).ToList();
            
            if (points.Count == 0)
                return;

            // Удаляем последовательные дубликаты точек из списка
            var cleanedPoints = new List<(double x, double y)>();
            double tolerance = 1e-6;
            
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                
                // Добавляем точку только если она отличается от предыдущей
                if (cleanedPoints.Count == 0 || 
                    Math.Abs(cleanedPoints[cleanedPoints.Count - 1].x - point.x) > tolerance ||
                    Math.Abs(cleanedPoints[cleanedPoints.Count - 1].y - point.y) > tolerance)
                {
                    cleanedPoints.Add(point);
                }
            }
            
            if (cleanedPoints.Count == 0)
                return;

            // Находим ближайшую точку к текущей позиции
            int currentIndex = 0;
            double minDistance = double.MaxValue;
            
            for (int i = 0; i < cleanedPoints.Count; i++)
            {
                double dx = cleanedPoints[i].x - currentPosition.x;
                double dy = cleanedPoints[i].y - currentPosition.y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < minDistance)
                {
                    minDistance = distance;
                    currentIndex = i;
                }
            }

            // Если мы находимся очень близко к найденной точке, начинаем со следующей
            // Иначе обрабатываем все точки с начала
            int startIndex = (minDistance < tolerance && currentIndex < cleanedPoints.Count - 1) 
                ? currentIndex + 1 
                : 0;

            // Обрабатываем все точки контура последовательно, начиная с startIndex
            for (int i = startIndex; i < cleanedPoints.Count; i++)
            {
                var point = cleanedPoints[i];
                addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
            
            // Если мы начали не с начала, обрабатываем точки от начала до startIndex
            if (startIndex > 0)
            {
                for (int i = 0; i < startIndex; i++)
                {
                    var point = cleanedPoints[i];
                    addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
            }
            
            // Замыкаем контур - возвращаемся к первой точке, если она не совпадает с последней
            if (cleanedPoints.Count > 1)
            {
                var firstPoint = cleanedPoints[0];
                var lastPoint = cleanedPoints[cleanedPoints.Count - 1];
                
                if (Math.Abs(firstPoint.x - lastPoint.x) > tolerance || 
                    Math.Abs(firstPoint.y - lastPoint.y) > tolerance)
                {
                    addLine($"{g1} X{firstPoint.x.ToString(fmt, culture)} Y{firstPoint.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                }
            }
        }

        /// <summary>
        /// Генерирует контур из точек для DXF операций.
        /// Группирует полилинии в контуры и обрабатывает каждый контур отдельно.
        /// </summary>
        private void GenerateDxfContourFromPoints(
            ProfileDxfOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            (double x, double y) currentPosition,
            double workingZ,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;
            double tolerance = 1e-6;

            if (op.Polylines == null || op.Polylines.Count == 0)
                return;

            var toolRadius = op.ToolDiameter / 2.0;
            var offset = 0.0;
            if (op.ToolPathMode == ToolPathMode.Outside)
                offset = toolRadius;
            else if (op.ToolPathMode == ToolPathMode.Inside)
                offset = -toolRadius;

            // Группируем полилинии в контуры
            var contours = GroupPolylinesIntoContours(op.Polylines, tolerance);

            // Обрабатываем каждый контур отдельно
            bool isFirstContour = true;
            foreach (var contour in contours)
            {
                if (contour == null || contour.Count == 0)
                    continue;

                // Собираем все точки контура последовательно
                var allContourPoints = new List<(double x, double y)>();

                foreach (var polyline in contour)
                {
                    if (polyline?.Points == null || polyline.Points.Count < 2)
                        continue;

                    // Применяем смещение к точкам полилинии
                    var points = polyline.Points;
                    var offsetPoints = new List<(double x, double y)>();

                    for (int i = 0; i < points.Count; i++)
                    {
                        var p = points[i];
                        
                        DxfPoint prevP, nextP;
                        if (i == 0)
                        {
                            prevP = points.Count > 1 ? points[points.Count - 1] : points[0];
                            nextP = points.Count > 1 ? points[1] : points[0];
                        }
                        else if (i == points.Count - 1)
                        {
                            prevP = points[i - 1];
                            nextP = points.Count > 2 ? points[0] : points[i - 1];
                        }
                        else
                        {
                            prevP = points[i - 1];
                            nextP = points[i + 1];
                        }

                        var dx1 = p.X - prevP.X;
                        var dy1 = p.Y - prevP.Y;
                        var dx2 = nextP.X - p.X;
                        var dy2 = nextP.Y - p.Y;

                        var len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                        var len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                        (double x, double y) offsetPoint;
                        if (len1 > tolerance && len2 > tolerance)
                        {
                            var nx1 = -dy1 / len1;
                            var ny1 = dx1 / len1;
                            var nx2 = -dy2 / len2;
                            var ny2 = dx2 / len2;

                            var nx = (nx1 + nx2) / 2.0;
                            var ny = (ny1 + ny2) / 2.0;
                            var nlen = Math.Sqrt(nx * nx + ny * ny);
                            if (nlen > tolerance)
                            {
                                nx /= nlen;
                                ny /= nlen;
                            }

                            offsetPoint = (p.X + nx * offset, p.Y + ny * offset);
                        }
                        else if (len1 > tolerance)
                        {
                            var nx = -dy1 / len1;
                            var ny = dx1 / len1;
                            offsetPoint = (p.X + nx * offset, p.Y + ny * offset);
                        }
                        else if (len2 > tolerance)
                        {
                            var nx = -dy2 / len2;
                            var ny = dx2 / len2;
                            offsetPoint = (p.X + nx * offset, p.Y + ny * offset);
                        }
                        else
                        {
                            offsetPoint = (p.X, p.Y);
                        }
                        
                        offsetPoints.Add(offsetPoint);
                    }

                    if (offsetPoints.Count == 0)
                        continue;

                    // Проверяем, замкнута ли полилиния
                    bool isPolylineClosed = offsetPoints.Count > 1 && 
                        Math.Abs(offsetPoints[0].x - offsetPoints[offsetPoints.Count - 1].x) < tolerance &&
                        Math.Abs(offsetPoints[0].y - offsetPoints[offsetPoints.Count - 1].y) < tolerance;

                    int pointsToAdd = isPolylineClosed ? offsetPoints.Count - 1 : offsetPoints.Count;

                    // Добавляем точки полилинии в контур последовательно
                    // Пропускаем первую точку, если она совпадает с последней точкой предыдущей полилинии
                    int startIdx = 0;
                    if (allContourPoints.Count > 0 && pointsToAdd > 0)
                    {
                        var lastPoint = allContourPoints[allContourPoints.Count - 1];
                        var firstPoint = offsetPoints[0];
                        if (Math.Abs(lastPoint.x - firstPoint.x) < tolerance &&
                            Math.Abs(lastPoint.y - firstPoint.y) < tolerance)
                        {
                            startIdx = 1; // Пропускаем первую точку, так как она совпадает с последней
                        }
                    }

                    for (int i = startIdx; i < pointsToAdd; i++)
                    {
                        allContourPoints.Add(offsetPoints[i]);
                    }
                }

                if (allContourPoints.Count == 0)
                    continue;

                // Поднимаем инструмент перед переходом к следующему контуру (кроме первого)
                if (!isFirstContour && allContourPoints.Count > 0)
                {
                    // Поднимаем инструмент на безопасную высоту перед переходом к следующему контуру
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    
                    // Перемещаемся к начальной точке следующего контура на безопасной высоте
                    var firstPoint = allContourPoints[0];
                    addLine($"{g0} X{firstPoint.x.ToString(fmt, culture)} Y{firstPoint.y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    
                    // Опускаемся на рабочую высоту
                    addLine($"{g1} Z{workingZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");
                }

                // Генерируем G-code для контура
                // Строим линии только между соседними точками внутри контура
                if (op.Direction == MillingDirection.Clockwise)
                {
                    // По часовой стрелке: от последней к первой
                    for (int i = allContourPoints.Count - 1; i >= 0; i--)
                    {
                        var point = allContourPoints[i];
                        addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                }
                else
                {
                    // Против часовой стрелки: от первой к последней
                    for (int i = 0; i < allContourPoints.Count; i++)
                    {
                        var point = allContourPoints[i];
                        addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }
                }

                // Отмечаем, что первый контур обработан
                isFirstContour = false;
            }
        }

        /// <summary>
        /// Группирует полилинии в контуры по соединениям.
        /// </summary>
        private List<List<DxfPolyline>> GroupPolylinesIntoContours(List<DxfPolyline> polylines, double tolerance)
        {
            var contours = new List<List<DxfPolyline>>();
            var used = new bool[polylines.Count];

            for (int i = 0; i < polylines.Count; i++)
            {
                if (used[i] || polylines[i]?.Points == null || polylines[i].Points.Count < 2)
                    continue;

                // Начинаем новый контур с этой полилинии
                var contour = BuildContourFromPolyline(polylines, i, used, tolerance);
                if (contour != null && contour.Count > 0)
                {
                    contours.Add(contour);
                }
            }

            return contours;
        }

        /// <summary>
        /// Строит контур, начиная с указанной полилинии.
        /// </summary>
        private List<DxfPolyline> BuildContourFromPolyline(List<DxfPolyline> polylines, int startIdx, bool[] used, double tolerance)
        {
            var contour = new List<DxfPolyline> { polylines[startIdx] };
            used[startIdx] = true;

            var startPoint = polylines[startIdx].Points[0];
            var currentPoint = polylines[startIdx].Points[polylines[startIdx].Points.Count - 1];

            // Ищем следующие полилинии, соединенные с текущим контуром
            bool foundConnection = true;
            while (foundConnection)
            {
                foundConnection = false;

                for (int i = 0; i < polylines.Count; i++)
                {
                    if (used[i] || polylines[i]?.Points == null || polylines[i].Points.Count < 2)
                        continue;

                    var polyline = polylines[i];
                    var polyStart = polyline.Points[0];
                    var polyEnd = polyline.Points[polyline.Points.Count - 1];

                    // Проверяем соединение с текущей точкой контура
                    if (PointsMatch(currentPoint, polyStart, tolerance))
                    {
                        contour.Add(polyline);
                        used[i] = true;
                        currentPoint = polyEnd;
                        foundConnection = true;
                        break;
                    }
                    else if (PointsMatch(currentPoint, polyEnd, tolerance))
                    {
                        // Нужно добавить полилинию в обратном порядке
                        var reversedPolyline = new DxfPolyline
                        {
                            Points = new List<DxfPoint>(polyline.Points)
                        };
                        reversedPolyline.Points.Reverse();
                        contour.Add(reversedPolyline);
                        used[i] = true;
                        currentPoint = polyStart;
                        foundConnection = true;
                        break;
                    }
                }

                // Проверяем, замкнулся ли контур
                if (PointsMatch(currentPoint, startPoint, tolerance))
                {
                    break;
                }
            }

            return contour;
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2, double tolerance)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
        }

        /// <summary>
        /// Генерирует контур с использованием дуг.
        /// </summary>
        private void GenerateContourWithArcs(
            IProfileOperation op,
            IProfileGeometry geometry,
            double toolOffset,
            System.Collections.Generic.List<IArcSegment> arcSegments,
            Action<string> addLine,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            var g2 = settings.UsePaddedGCodes ? "G02" : "G2";
            var g3 = settings.UsePaddedGCodes ? "G03" : "G3";

            foreach (var arc in arcSegments)
            {
                var arcCommand = arc.IsClockwise ? g2 : g3;
                var i = arc.Center.x - arc.StartPoint.x;
                var j = arc.Center.y - arc.StartPoint.y;

                addLine($"{arcCommand} X{arc.EndPoint.x.ToString(fmt, culture)} Y{arc.EndPoint.y.ToString(fmt, culture)} I{i.ToString(fmt, culture)} J{j.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }
    }
}

