using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketDxfOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation,
                             Action<string> addLine,
                             string g0,
                             string g1,
                             GCodeSettings settings)
        {
            var op = operation as PocketDxfOperation;
            if (op == null)
                return;

            var fmt = $"0.{new string('0', Math.Max(0, op.Decimals))}";
            var culture = CultureInfo.InvariantCulture;

            if (op.ClosedContours == null || op.ClosedContours.Count == 0)
            {
                addLine("(DXF pocket operation has no closed contours)");
                return;
            }

            var toolRadius = op.ToolDiameter / 2.0;
            var step = op.ToolDiameter * (op.StepPercentOfTool <= 0
                                                ? 0.4
                                                : op.StepPercentOfTool / 100.0);
            if (step < 1e-6) step = op.ToolDiameter * 0.4;

            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var pass = 0;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            // Обрабатываем каждый замкнутый контур
            foreach (var contour in op.ClosedContours)
            {
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

                // Дополнительная проверка, что контур действительно замкнут
                if (!IsClosedContour(contour))
                {
                    if (settings.UseComments)
                        addLine("(Skipping non-closed contour)");
                    continue;
                }

                currentZ = op.ContourHeight;
                pass = 0;

                while (currentZ > finalZ)
                {
                    var nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    var depthFromTop = op.ContourHeight - nextZ;
                    var offset = depthFromTop * taperTan;
                    var effectiveToolRadius = toolRadius + offset;
                    if (effectiveToolRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    if (settings.UseComments)
                        addLine($"(Contour pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    // Переходы в безопасную высоту перед генерацией траектории
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    // Генерируем траекторию кармана в зависимости от стратегии
                    switch (op.PocketStrategy)
                    {
                        case PocketStrategy.Spiral:
                            GenerateSpiralContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        case PocketStrategy.Concentric:
                            GenerateConcentricContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        case PocketStrategy.Radial:
                            GenerateRadialContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        case PocketStrategy.Lines:
                            GenerateLinesContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, op.LineAngleDeg, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        case PocketStrategy.ZigZag:
                            GenerateZigZagContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, op.LineAngleDeg, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        default:
                            // По умолчанию используем концентрические контуры
                            GenerateConcentricContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                    }

                    // В конце прохода слоя не поднимаем фрезу прямо на внешнем контуре:
                    // сначала уходим к центру контура, затем поднимаем фрезу.
                    var center = GetContourCenter(contour);
                    addLine($"{g1} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    if (nextZ > finalZ)
                    {
                        var retractZAfterPass = nextZ + op.RetractHeight;
                        addLine($"{g0} Z{retractZAfterPass.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    }

                    currentZ = nextZ;
                }

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            }
        }

        private DxfPoint GetContourCenter(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count == 0)
                return new DxfPoint { X = 0, Y = 0 };

            double sumX = 0, sumY = 0;
            foreach (var p in contour.Points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            return new DxfPoint
            {
                X = sumX / contour.Points.Count,
                Y = sumY / contour.Points.Count
            };
        }

        private void GenerateConcentricContours(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, DxfPolyline outerContour,
            double toolRadius, double step, double feedXYRapid, double feedXYWork,
            MillingDirection direction, double currentZ, double safeZ, double feedZRapid)
        {
            // Генерируем концентрические контуры, уменьшая контур на step
            // Аналогично GenerateConcentricRectangles, но для произвольных контуров
            
            // Вычисляем максимальный радиус контура (расстояние от центра до самой дальней точки)
            var center = GetContourCenter(outerContour);
            double maxRadius = 0;
            foreach (var p in outerContour.Points)
            {
                var dx = p.X - center.X;
                var dy = p.Y - center.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > maxRadius)
                    maxRadius = dist;
            }
            
            // Генерируем список смещений (от внешнего к внутреннему)
            var offsets = new List<double>();
            var maxOffset = maxRadius - toolRadius - 1e-6;
            for (double o = 0; o <= maxOffset; o += step)
                offsets.Add(o);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxOffset)
                offsets.Add(maxOffset);
            offsets = offsets.OrderBy(v => v).ToList(); // от наружного к внутреннему (0 – внешний)

            bool firstContour = true;
            foreach (var offset in offsets)
            {
                // Смещаем контур внутрь на (toolRadius + offset)
                var currentContour = OffsetContour(outerContour, -(toolRadius + offset));
                if (currentContour == null || currentContour.Points == null || currentContour.Points.Count < 3)
                    break;

                var area = GetContourArea(currentContour);
                if (area <= step * step) // Минимальная площадь
                    break;

                // Переход к началу текущего контура
                var startPoint = GetContourStartPoint(currentContour, direction);
                if (firstContour)
                {
                    // Для первого контура - переход и опускание инструмента
                    addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    firstContour = false;
                }
                else
                {
                    // Для последующих контуров - подъем, переход и опускание
                    addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                }

                // Проходим по текущему контуру
                GenerateContourPath(addLine, g1, fmt, culture, currentContour, feedXYWork, direction);
            }
        }

        private void GenerateSpiralContours(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, DxfPolyline outerContour,
            double toolRadius, double step, double feedXYRapid, double feedXYWork,
            MillingDirection direction, double currentZ, double safeZ, double feedZRapid)
        {
            // Генерируем спираль Архимеда из центра контура
            var center = GetContourCenter(outerContour);
            var offsetContour = OffsetContour(outerContour, -toolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return;

            // Вычисляем максимальный радиус контура
            double maxRadius = 0;
            foreach (var p in offsetContour.Points)
            {
                var dx = p.X - center.X;
                var dy = p.Y - center.Y;
                var dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > maxRadius)
                    maxRadius = dist;
            }

            const double a = 0.0;                        // r(θ) = a + b·θ
            double b = step / (2 * Math.PI);             // радиальная скорость за один оборот

            int pointsPerRevolution = 128;
            double angleStep = 2 * Math.PI / pointsPerRevolution;
            double θMax = (maxRadius - a) / b;

            // Функция проверки, находится ли точка внутри контура
            bool IsPointInside(double x, double y)
            {
                return IsPointInsideContour(x, y, offsetContour);
            }

            // Функция нахождения точки пересечения отрезка с контуром
            DxfPoint FindContourIntersection(double x1, double y1, double x2, double y2)
            {
                DxfPoint closestIntersection = null;
                double minDist = double.MaxValue;
                double dx = x2 - x1;
                double dy = y2 - y1;

                for (int i = 0; i < offsetContour.Points.Count; i++)
                {
                    var p1 = offsetContour.Points[i];
                    var p2 = offsetContour.Points[(i + 1) % offsetContour.Points.Count];

                    // Находим пересечение отрезка (x1, y1) - (x2, y2) с отрезком (p1, p2)
                    var intersection = LineSegmentIntersection(x1, y1, dx, dy, p1.X, p1.Y, p2.X, p2.Y);
                    if (intersection != null)
                    {
                        var dist = Math.Sqrt(Math.Pow(intersection.X - x1, 2) + Math.Pow(intersection.Y - y1, 2));
                        if (dist < minDist && dist > 1e-6) // Игнорируем очень близкие точки
                        {
                            minDist = dist;
                            closestIntersection = intersection;
                        }
                    }
                }

                return closestIntersection ?? new DxfPoint { X = x2, Y = y2 };
            }

            // Переход к центру и опускание инструмента
            addLine($"{g0} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            addLine($"{g1} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

            // Генерируем точки спирали
            var spiralPoints = new List<(double x, double y)>();
            spiralPoints.Add((center.X, center.Y));

            for (double θ = angleStep; θ <= θMax + 1e-9; θ += angleStep)
            {
                double r = a + b * θ;
                double xSpiral = center.X + r * Math.Cos(θ);
                double ySpiral = center.Y + r * Math.Sin(θ);
                spiralPoints.Add((xSpiral, ySpiral));
            }

            // Обрабатываем точки спирали
            double prevX = center.X, prevY = center.Y;
            bool prevInside = true;
            DxfPoint lastExitPoint = null;

            for (int i = 1; i < spiralPoints.Count; i++)
            {
                var point = spiralPoints[i];
                double xSpiral = point.x;
                double ySpiral = point.y;
                bool currentInside = IsPointInside(xSpiral, ySpiral);

                if (prevInside && currentInside)
                {
                    // Обе точки внутри - просто добавляем
                    addLine($"{g1} X{xSpiral.ToString(fmt, culture)} Y{ySpiral.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    prevX = xSpiral;
                    prevY = ySpiral;
                }
                else if (prevInside && !currentInside)
                {
                    // Выход из контура - находим точку выхода
                    var exit = FindContourIntersection(prevX, prevY, xSpiral, ySpiral);
                    addLine($"{g1} X{exit.X.ToString(fmt, culture)} Y{exit.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    lastExitPoint = exit;
                    prevInside = false;
                    prevX = xSpiral;
                    prevY = ySpiral;
                }
                else if (!prevInside && currentInside)
                {
                    // Вход в контур - находим точку входа
                    var prevPoint = spiralPoints[i - 1];
                    var entry = FindContourIntersection(prevPoint.x, prevPoint.y, xSpiral, ySpiral);
                    
                    if (lastExitPoint != null)
                    {
                        // Двигаемся по контуру от точки выхода до точки входа
                        MoveAlongContour(addLine, g0, g1, fmt, culture, lastExitPoint.X, lastExitPoint.Y,
                            entry.X, entry.Y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid);
                        lastExitPoint = null;
                    }
                    
                    addLine($"{g1} X{xSpiral.ToString(fmt, culture)} Y{ySpiral.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    prevX = xSpiral;
                    prevY = ySpiral;
                    prevInside = true;
                }
                else
                {
                    // Обе точки снаружи - обновляем предыдущую точку
                    prevX = xSpiral;
                    prevY = ySpiral;
                    prevInside = false;
                }
            }

            // Финальный проход по внешнему контуру
            var startPoint = GetContourStartPoint(offsetContour, direction);
            if (lastExitPoint != null)
            {
                // Двигаемся по контуру от последней точки выхода до начальной точки
                MoveAlongContour(addLine, g0, g1, fmt, culture, lastExitPoint.X, lastExitPoint.Y,
                    startPoint.X, startPoint.Y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid);
            }
            else
            {
                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            }
            GenerateContourPath(addLine, g1, fmt, culture, offsetContour, feedXYWork, direction);
        }

        private void MoveAlongContour(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, double x1, double y1, double x2, double y2,
            DxfPolyline contour, MillingDirection direction, double feedXYRapid, double feedXYWork,
            double currentZ, double safeZ, double feedZRapid)
        {
            // Находим ближайшие точки на контуре
            int idx1 = FindNearestPointOnContour(x1, y1, contour);
            int idx2 = FindNearestPointOnContour(x2, y2, contour);

            if (idx1 < 0 || idx2 < 0)
            {
                // Если не удалось найти точки, поднимаем инструмент
                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                return;
            }

            // Двигаемся по контуру от idx1 до idx2
            int step = direction == MillingDirection.Clockwise ? -1 : 1;
            int currentIdx = idx1;
            int targetIdx = idx2;

            while (currentIdx != targetIdx)
            {
                currentIdx = (currentIdx + step + contour.Points.Count) % contour.Points.Count;
                var p = contour.Points[currentIdx];
                addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }

            // Двигаемся к конечной точке
            addLine($"{g1} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
        }

        private int FindNearestPointOnContour(double x, double y, DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count == 0)
                return -1;

            int nearestIdx = 0;
            double minDist = double.MaxValue;

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p = contour.Points[i];
                var dist = Math.Sqrt(Math.Pow(p.X - x, 2) + Math.Pow(p.Y - y, 2));
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIdx = i;
                }
            }

            return nearestIdx;
        }

        private DxfPoint GetContourStartPoint(DxfPolyline contour, MillingDirection direction)
        {
            if (contour?.Points == null || contour.Points.Count == 0)
                return new DxfPoint { X = 0, Y = 0 };

            // Находим самую нижнюю точку (или самую верхнюю для обратного направления)
            var startPoint = contour.Points[0];
            foreach (var p in contour.Points)
            {
                if (direction == MillingDirection.Clockwise)
                {
                    if (p.Y < startPoint.Y || (Math.Abs(p.Y - startPoint.Y) < 1e-6 && p.X < startPoint.X))
                        startPoint = p;
                }
                else
                {
                    if (p.Y > startPoint.Y || (Math.Abs(p.Y - startPoint.Y) < 1e-6 && p.X > startPoint.X))
                        startPoint = p;
                }
            }
            return startPoint;
        }

        private void GenerateRadialContours(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, DxfPolyline outerContour,
            double toolRadius, double step, double feedXYRapid, double feedXYWork,
            MillingDirection direction, double currentZ, double safeZ, double feedZRapid)
        {
            // Для радиальной стратегии генерируем радиальные линии от центра до контура
            var center = GetContourCenter(outerContour);
            var offsetContour = OffsetContour(outerContour, -toolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return;

            // Вычисляем периметр контура для определения количества сегментов
            double perimeter = 0;
            for (int i = 0; i < offsetContour.Points.Count; i++)
            {
                var p1 = offsetContour.Points[i];
                var p2 = offsetContour.Points[(i + 1) % offsetContour.Points.Count];
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                perimeter += Math.Sqrt(dx * dx + dy * dy);
            }

            int segments = Math.Max(16, (int)Math.Ceiling(perimeter / step));
            double angleStep = 2 * Math.PI / segments * ((direction == MillingDirection.Clockwise) ? -1 : 1);

            // Переход к центру и опускание инструмента
            addLine($"{g0} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");

            // Генерируем радиальные линии
            for (int i = 0; i < segments; i++)
            {
                double angle = angleStep * i;
                double dx = Math.Cos(angle);
                double dy = Math.Sin(angle);

                // Находим пересечение луча с контуром
                var intersection = FindRayContourIntersection(center.X, center.Y, dx, dy, offsetContour);
                if (intersection != null)
                {
                    var hit = intersection;
                    // Двигаемся от центра к контуру
                    addLine($"{g1} X{hit.X.ToString(fmt, culture)} Y{hit.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    
                    // Двигаемся по контуру на step
                    var nextPoint = GetPointOnContourAtDistance(offsetContour, hit.X, hit.Y, step, direction);
                    if (nextPoint != null)
                    {
                        addLine($"{g1} X{nextPoint.X.ToString(fmt, culture)} Y{nextPoint.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    }
                    
                    // Возвращаемся в центр
                    addLine($"{g1} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
            }

            // Финальный проход по внешнему контуру
            var startPoint = GetContourStartPoint(offsetContour, direction);
            addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            GenerateContourPath(addLine, g1, fmt, culture, offsetContour, feedXYWork, direction);
        }

        private void GenerateLinesContours(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, DxfPolyline outerContour,
            double toolRadius, double step, double feedXYRapid, double feedXYWork,
            MillingDirection direction, double lineAngleDeg, double currentZ, double safeZ, double feedZRapid)
        {
            // Генерируем параллельные линии под заданным углом
            var offsetContour = OffsetContour(outerContour, -toolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return;

            // Вычисляем bounding box контура
            double minX = offsetContour.Points.Min(p => p.X);
            double maxX = offsetContour.Points.Max(p => p.X);
            double minY = offsetContour.Points.Min(p => p.Y);
            double maxY = offsetContour.Points.Max(p => p.Y);

            double lineAngleRad = lineAngleDeg * Math.PI / 180.0;
            double dirX = Math.Cos(lineAngleRad);
            double dirY = Math.Sin(lineAngleRad);
            double nx = -dirY; // Нормаль к направлению линий
            double ny = dirX;

            // Вычисляем проекции углов bounding box на нормаль
            var corners = new[]
            {
                (minX, minY),
                (maxX, minY),
                (maxX, maxY),
                (minX, maxY)
            };
            double minProj = corners.Min(c => c.Item1 * nx + c.Item2 * ny);
            double maxProj = corners.Max(c => c.Item1 * nx + c.Item2 * ny);

            // Генерируем смещения для линий
            var offsets = new List<double>();
            for (double t = minProj; t <= maxProj + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxProj - 1e-6)
                offsets.Add(maxProj);

            bool firstLine = true;
            foreach (var t in offsets)
            {
                // Находим пересечения прямой с контуром
                var intersections = FindLineContourIntersections(nx * t, ny * t, dirX, dirY, offsetContour);
                if (intersections.Count < 2)
                    continue;

                // Сортируем точки по параметру s (вдоль линии)
                intersections = intersections.OrderBy(p => p.s).ToList();
                var start = intersections[0];
                var end = intersections[intersections.Count - 1];

                if (firstLine)
                {
                    addLine($"{g0} X{start.x.ToString(fmt, culture)} Y{start.y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    firstLine = false;
                }
                else
                {
                    addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{start.x.ToString(fmt, culture)} Y{start.y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                }

                addLine($"{g1} X{end.x.ToString(fmt, culture)} Y{end.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }

            // Финальный проход по внешнему контуру
            var startPoint = GetContourStartPoint(offsetContour, direction);
            addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            GenerateContourPath(addLine, g1, fmt, culture, offsetContour, feedXYWork, direction);
        }

        private void GenerateZigZagContours(Action<string> addLine, string g0, string g1,
            string fmt, CultureInfo culture, DxfPolyline outerContour,
            double toolRadius, double step, double feedXYRapid, double feedXYWork,
            MillingDirection direction, double lineAngleDeg, double currentZ, double safeZ, double feedZRapid)
        {
            // Генерируем зигзаг - параллельные линии с чередованием направления
            var offsetContour = OffsetContour(outerContour, -toolRadius);
            if (offsetContour == null || offsetContour.Points == null || offsetContour.Points.Count < 3)
                return;

            // Вычисляем bounding box контура
            double minX = offsetContour.Points.Min(p => p.X);
            double maxX = offsetContour.Points.Max(p => p.X);
            double minY = offsetContour.Points.Min(p => p.Y);
            double maxY = offsetContour.Points.Max(p => p.Y);

            double lineAngleRad = lineAngleDeg * Math.PI / 180.0;
            double dirX = Math.Cos(lineAngleRad);
            double dirY = Math.Sin(lineAngleRad);
            double nx = -dirY;
            double ny = dirX;

            var corners = new[]
            {
                (minX, minY),
                (maxX, minY),
                (maxX, maxY),
                (minX, maxY)
            };
            double minProj = corners.Min(c => c.Item1 * nx + c.Item2 * ny);
            double maxProj = corners.Max(c => c.Item1 * nx + c.Item2 * ny);

            var offsets = new List<double>();
            for (double t = minProj; t <= maxProj + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxProj - 1e-6)
                offsets.Add(maxProj);

            bool firstLine = true;
            for (int i = 0; i < offsets.Count; i++)
            {
                var t = offsets[i];
                var intersections = FindLineContourIntersections(nx * t, ny * t, dirX, dirY, offsetContour);
                if (intersections.Count < 2)
                    continue;

                intersections = intersections.OrderBy(p => p.s).ToList();
                var start = intersections[0];
                var end = intersections[intersections.Count - 1];

                // Чередуем направление для зигзага
                if (i % 2 == 1)
                {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }

                if (firstLine)
                {
                    addLine($"{g0} X{start.x.ToString(fmt, culture)} Y{start.y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    firstLine = false;
                }
                else
                {
                    addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{start.x.ToString(fmt, culture)} Y{start.y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                }

                addLine($"{g1} X{end.x.ToString(fmt, culture)} Y{end.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                // Переход к следующей линии по контуру (если не последняя)
                if (i + 1 < offsets.Count)
                {
                    var nextT = offsets[i + 1];
                    var nextIntersections = FindLineContourIntersections(nx * nextT, ny * nextT, dirX, dirY, offsetContour);
                    if (nextIntersections.Count >= 2)
                    {
                        nextIntersections = nextIntersections.OrderBy(p => p.s).ToList();
                        var nextStart = (i + 1) % 2 == 0 ? nextIntersections[0] : nextIntersections[nextIntersections.Count - 1];
                        
                        // Двигаемся по контуру от конца текущей линии до начала следующей
                        MoveAlongContour(addLine, g0, g1, fmt, culture, end.x, end.y,
                            nextStart.x, nextStart.y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid);
                    }
                }
            }

            // Финальный проход по внешнему контуру
            var startPoint = GetContourStartPoint(offsetContour, direction);
            addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{startPoint.X.ToString(fmt, culture)} Y{startPoint.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            GenerateContourPath(addLine, g1, fmt, culture, offsetContour, feedXYWork, direction);
        }

        private List<(double x, double y, double s)> FindLineContourIntersections(double lineX0, double lineY0, double dirX, double dirY, DxfPolyline contour)
        {
            var intersections = new List<(double x, double y, double s)>();

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];

                // Параметрическое уравнение линии: (lineX0, lineY0) + s * (dirX, dirY)
                // Параметрическое уравнение отрезка: (p1.X, p1.Y) + t * (p2.X - p1.X, p2.Y - p1.Y), t in [0, 1]

                double segDx = p2.X - p1.X;
                double segDy = p2.Y - p1.Y;

                double denom = dirX * segDy - dirY * segDx;
                if (Math.Abs(denom) < 1e-9)
                    continue; // Параллельные линии

                double t = ((p1.X - lineX0) * dirY - (p1.Y - lineY0) * dirX) / denom;
                if (t < 0 || t > 1)
                    continue; // Пересечение вне отрезка

                double s = ((p1.X - lineX0) * segDy - (p1.Y - lineY0) * segDx) / denom;

                double x = lineX0 + s * dirX;
                double y = lineY0 + s * dirY;

                intersections.Add((x, y, s));
            }

            return intersections;
        }

        private DxfPoint FindRayContourIntersection(double startX, double startY, double dx, double dy, DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 2)
                return null;

            DxfPoint closestIntersection = null;
            double minDist = double.MaxValue;

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];

                // Находим пересечение луча с отрезком
                var intersection = LineSegmentIntersection(startX, startY, dx, dy, p1.X, p1.Y, p2.X, p2.Y);
                if (intersection != null)
                {
                    var dist = Math.Sqrt(Math.Pow(intersection.X - startX, 2) + Math.Pow(intersection.Y - startY, 2));
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestIntersection = intersection;
                    }
                }
            }

            return closestIntersection;
        }

        private DxfPoint LineSegmentIntersection(double rayX, double rayY, double rayDx, double rayDy,
            double segX1, double segY1, double segX2, double segY2)
        {
            // Параметрическое уравнение луча: (rayX, rayY) + t * (rayDx, rayDy), t >= 0
            // Параметрическое уравнение отрезка: (segX1, segY1) + s * (segX2 - segX1, segY2 - segY1), s in [0, 1]

            double segDx = segX2 - segX1;
            double segDy = segY2 - segY1;

            double denom = rayDx * segDy - rayDy * segDx;
            if (Math.Abs(denom) < 1e-9)
                return null; // Параллельные линии

            double t = ((segX1 - rayX) * segDy - (segY1 - rayY) * segDx) / denom;
            if (t < 0)
                return null; // Пересечение за началом луча

            double s = ((segX1 - rayX) * rayDy - (segY1 - rayY) * rayDx) / denom;
            if (s < 0 || s > 1)
                return null; // Пересечение вне отрезка

            return new DxfPoint
            {
                X = rayX + t * rayDx,
                Y = rayY + t * rayDy
            };
        }

        private DxfPoint GetPointOnContourAtDistance(DxfPolyline contour, double startX, double startY, double distance, MillingDirection direction)
        {
            if (contour?.Points == null || contour.Points.Count < 2)
                return null;

            // Находим ближайшую точку на контуре к startX, startY
            int startIdx = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p = contour.Points[i];
                var dist = Math.Sqrt(Math.Pow(p.X - startX, 2) + Math.Pow(p.Y - startY, 2));
                if (dist < minDist)
                {
                    minDist = dist;
                    startIdx = i;
                }
            }

            // Двигаемся по контуру на расстояние distance
            double remainingDist = distance;
            int currentIdx = startIdx;
            int step = direction == MillingDirection.Clockwise ? -1 : 1;

            while (remainingDist > 1e-6 && currentIdx >= 0 && currentIdx < contour.Points.Count)
            {
                var p1 = contour.Points[currentIdx];
                var p2 = contour.Points[(currentIdx + step + contour.Points.Count) % contour.Points.Count];
                
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                var segLen = Math.Sqrt(dx * dx + dy * dy);

                if (segLen <= remainingDist)
                {
                    remainingDist -= segLen;
                    currentIdx = (currentIdx + step + contour.Points.Count) % contour.Points.Count;
                }
                else
                {
                    // Точка находится на текущем сегменте
                    var t = remainingDist / segLen;
                    return new DxfPoint
                    {
                        X = p1.X + t * dx,
                        Y = p1.Y + t * dy
                    };
                }
            }

            return contour.Points[currentIdx];
        }


        private void GenerateContourPath(Action<string> addLine, string g1,
            string fmt, CultureInfo culture, DxfPolyline contour,
            double feedXY, MillingDirection direction)
        {
            if (contour?.Points == null || contour.Points.Count < 2)
                return;

            var points = contour.Points.ToList();
            if (direction == MillingDirection.CounterClockwise)
            {
                points.Reverse();
            }

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXY.ToString(fmt, culture)}");
            }
        }

        private DxfPolyline OffsetContour(DxfPolyline contour, double offset)
        {
            // Упрощенное смещение контура: смещаем каждую точку по нормали внутрь
            // Это упрощенная версия, для полноценной работы нужна библиотека для работы с полигонами
            if (contour?.Points == null || contour.Points.Count < 3)
                return null;

            // Определяем направление обхода контура по знаку площади
            double signedArea = 0;
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                signedArea += p1.X * p2.Y - p2.X * p1.Y;
            }
            bool isClockwise = signedArea < 0; // Отрицательная площадь = по часовой стрелке

            var result = new DxfPolyline { Points = new List<DxfPoint>() };
            double absOffset = Math.Abs(offset);

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var prev = contour.Points[(i - 1 + contour.Points.Count) % contour.Points.Count];
                var curr = contour.Points[i];
                var next = contour.Points[(i + 1) % contour.Points.Count];

                // Вычисляем направление ребер
                var dx1 = curr.X - prev.X;
                var dy1 = curr.Y - prev.Y;
                var len1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                if (len1 < 1e-6) len1 = 1.0;
                
                var dx2 = next.X - curr.X;
                var dy2 = next.Y - curr.Y;
                var len2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                if (len2 < 1e-6) len2 = 1.0;

                // Нормали к ребрам (перпендикуляр, повернутый на 90° против часовой стрелки)
                var nx1 = -dy1 / len1;
                var ny1 = dx1 / len1;
                
                var nx2 = -dy2 / len2;
                var ny2 = dx2 / len2;
                
                // Средняя нормаль (биссектриса угла)
                var nx = (nx1 + nx2);
                var ny = (ny1 + ny2);
                var len = Math.Sqrt(nx * nx + ny * ny);
                if (len > 1e-6)
                {
                    nx /= len;
                    ny /= len;
                }
                else
                {
                    // Если нормали параллельны, используем одну из них
                    nx = nx1;
                    ny = ny1;
                }
                
                // Определяем направление нормали внутрь контура
                // Нормаль (-dy, dx) для ребра (dx, dy) направлена влево от направления обхода
                // Для контура по часовой стрелке нормаль влево = наружу, нужно инвертировать
                // Для контура против часовой стрелки нормаль влево = внутрь, используем как есть
                if (isClockwise)
                {
                    nx = -nx;
                    ny = -ny;
                }

                // Применяем смещение
                // Для отрицательного offset (смещение внутрь) используем нормаль как есть (уже направлена внутрь)
                // Для положительного offset (смещение наружу) инвертируем нормаль
                if (offset > 0)
                {
                    nx = -nx;
                    ny = -ny;
                }
                
                result.Points.Add(new DxfPoint
                {
                    X = curr.X + nx * absOffset,
                    Y = curr.Y + ny * absOffset
                });
            }

            return result;
        }

        private bool IsPointInsideContour(double x, double y, DxfPolyline contour)
        {
            // Ray casting algorithm для проверки, находится ли точка внутри полигона
            if (contour?.Points == null || contour.Points.Count < 3)
                return false;

            bool inside = false;
            for (int i = 0, j = contour.Points.Count - 1; i < contour.Points.Count; j = i++)
            {
                var pi = contour.Points[i];
                var pj = contour.Points[j];
                
                if (((pi.Y > y) != (pj.Y > y)) &&
                    (x < (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
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

        private bool IsClosedContour(DxfPolyline polyline)
        {
            if (polyline?.Points == null || polyline.Points.Count < 3)
                return false;

            var first = polyline.Points[0];
            var last = polyline.Points[polyline.Points.Count - 1];
            var dx = first.X - last.X;
            var dy = first.Y - last.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= 0.001; // Точность для определения замкнутости
        }
    }
}

