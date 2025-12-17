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

            bool roughing = op.IsRoughingEnabled;
            bool finishing = op.IsFinishingEnabled;
            double allowance = Math.Max(0.0, op.FinishAllowance);

            // Старое поведение, если режимы не включены
            if (!roughing && !finishing)
            {
                roughing = true;
                allowance = 0.0;
            }

            if (roughing)
            {
                var roughOp = CloneOp(op);
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, roughOp.TotalDepth - 1e-6));

                if (depthAllowance > 0)
                {
                    // Оставляем припуск по дну
                    roughOp.TotalDepth -= depthAllowance;
                    // Оставляем припуск по стенкам: эквивалентно увеличению радиуса фрезы
                    roughOp.ToolDiameter += 2 * depthAllowance;
                }

                GenerateCore(roughOp, addLine, g0, g1, settings);
            }

            if (finishing && allowance > 0)
            {
                var finishOp = CloneOp(op);
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, finishOp.TotalDepth));
                if (depthAllowance < 1e-6)
                    return;

                // Работаем только в слое припуска по глубине
                finishOp.ContourHeight = op.ContourHeight - (op.TotalDepth - depthAllowance);
                finishOp.TotalDepth = depthAllowance;
                finishOp.IsRoughingEnabled = false;
                finishOp.IsFinishingEnabled = false;
                finishOp.FinishAllowance = 0.0;

                // Для DXF пока все режимы (Walls/Bottom/All) ведут себя как "всё":
                // дорабатываем и дно, и стенки.
                GenerateCore(finishOp, addLine, g0, g1, settings);
            }
        }

        /// <summary>
        /// Базовая генерация DXF-кармана (старое поведение), без учёта rough/finish-режимов.
        /// </summary>
        private void GenerateCore(PocketDxfOperation op,
                                  Action<string> addLine,
                                  string g0,
                                  string g1,
                                  GCodeSettings settings)
        {
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

            foreach (var contour in op.ClosedContours)
            {
                if (contour?.Points == null || contour.Points.Count < 3)
                    continue;

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

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    switch (op.PocketStrategy)
                    {
                        case PocketStrategy.Spiral:
                            GenerateSpiralContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid, op.RetractHeight);
                            break;
                        case PocketStrategy.Concentric:
                            GenerateConcentricContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                        case PocketStrategy.Radial:
                            GenerateRadialContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid, op.RetractHeight);
                            break;
                        case PocketStrategy.Lines:
                            GenerateLinesContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, op.LineAngleDeg, nextZ, op.SafeZHeight, op.FeedZRapid, op.RetractHeight);
                            break;
                        case PocketStrategy.ZigZag:
                            GenerateZigZagContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, op.LineAngleDeg, nextZ, op.SafeZHeight, op.FeedZRapid, op.RetractHeight);
                            break;
                        default:
                            GenerateConcentricContours(addLine, g0, g1, fmt, culture, contour, effectiveToolRadius, step,
                                op.FeedXYRapid, op.FeedXYWork, op.Direction, nextZ, op.SafeZHeight, op.FeedZRapid);
                            break;
                    }

                    var center = GetContourCenter(contour);
                    // Возврат в центр на холостом ходу с подъемом
                    double retractZAfterPass = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{retractZAfterPass.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    
                    if (nextZ <= finalZ)
                    {
                        // Последний слой - поднимаемся на безопасную высоту
                        addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    }
                    // Иначе остаемся на высоте отвода для следующего слоя

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
            MillingDirection direction, double currentZ, double safeZ, double feedZRapid, double retractHeight)
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
            double dirSign = direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            // Функция проверки, находится ли точка внутри контура
            bool IsPointInside(double x, double y)
            {
                return IsPointInsideContour(x, y, offsetContour);
            }

            // Надёжное пересечение двух ОТРЕЗКОВ, а не луча:
            // (x1,y1)-(x2,y2) и (x3,y3)-(x4,y4). Возвращает null, если пересечения нет.
            DxfPoint SegmentSegmentIntersection(double x1, double y1, double x2, double y2,
                                                double x3, double y3, double x4, double y4)
            {
                double dx1 = x2 - x1;
                double dy1 = y2 - y1;
                double dx2 = x4 - x3;
                double dy2 = y4 - y3;

                double denom = dx1 * dy2 - dy1 * dx2;
                if (Math.Abs(denom) < 1e-9)
                    return null; // Параллельные или почти параллельные

                double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
                double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;

                const double tol = 1e-9;
                if (t1 < -tol || t1 > 1.0 + tol || t2 < -tol || t2 > 1.0 + tol)
                    return null; // Пересечение вне отрезков

                // Ограничиваем в пределах [0,1]
                t1 = Math.Max(0.0, Math.Min(1.0, t1));

                return new DxfPoint
                {
                    X = x1 + t1 * dx1,
                    Y = y1 + t1 * dy1
                };
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

                    // Находим пересечение ОТРЕЗКА (x1,y1)-(x2,y2) с отрезком (p1,p2).
                    // Раньше использовался луч, что могло дать пересечения "дальше" конца отрезка
                    // и приводить к скачкам траектории после одной ошибочной точки.
                    var intersection = SegmentSegmentIntersection(x1, y1, x2, y2, p1.X, p1.Y, p2.X, p2.Y);
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
                double ang = θ * dirSign;
                double xSpiral = center.X + r * Math.Cos(ang);
                double ySpiral = center.Y + r * Math.Sin(ang);
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
                            entry.X, entry.Y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid, retractHeight);
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
                    startPoint.X, startPoint.Y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid, retractHeight);
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
            double currentZ, double safeZ, double feedZRapid, double retractHeight)
        {
            // Находим ближайшие точки на контуре
            int idx1 = FindNearestPointOnContour(x1, y1, contour);
            int idx2 = FindNearestPointOnContour(x2, y2, contour);

            if (idx1 < 0 || idx2 < 0)
            {
                // Если не удалось найти точки, поднимаем инструмент
                double retractZ1 = currentZ + retractHeight;
                addLine($"{g0} Z{retractZ1.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                return;
            }

            // Поднимаем инструмент для перехода
            double retractZ = currentZ + retractHeight;
            addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");

            // Двигаемся по контуру от idx1 до idx2 на холостом ходу
            int step = direction == MillingDirection.Clockwise ? -1 : 1;
            int currentIdx = idx1;
            int targetIdx = idx2;

            while (currentIdx != targetIdx)
            {
                currentIdx = (currentIdx + step + contour.Points.Count) % contour.Points.Count;
                var p = contour.Points[currentIdx];
                addLine($"{g0} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            }

            // Двигаемся к конечной точке
            addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            
            // Опускаем обратно на рабочую высоту
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
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
            MillingDirection direction, double currentZ, double safeZ, double feedZRapid, double retractHeight)
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
                    
                    // Возвращаемся в центр на холостом ходу с подъемом
                    double retractZ = currentZ + retractHeight;
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{center.X.ToString(fmt, culture)} Y{center.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
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
            MillingDirection direction, double lineAngleDeg, double currentZ, double safeZ, double feedZRapid, double retractHeight)
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
                    // Поднимаем на retractHeight относительно текущей высоты
                    double retractZ = currentZ + retractHeight;
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
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
            MillingDirection direction, double lineAngleDeg, double currentZ, double safeZ, double feedZRapid, double retractHeight)
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
                    // Поднимаем на retractHeight относительно текущей высоты
                    double retractZ = currentZ + retractHeight;
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
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
                            nextStart.x, nextStart.y, offsetContour, direction, feedXYRapid, feedXYWork, currentZ, safeZ, feedZRapid, retractHeight);
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
            double absOffset = Math.Abs(offset);

            // Шаг 1: Делим контур на элементарные линии (отрезки между соседними точками)
            var elementaryLines = new List<DxfPolyline>();
            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];
                elementaryLines.Add(new DxfPolyline
                {
                    Points = new List<DxfPoint> { new DxfPoint { X = p1.X, Y = p1.Y }, new DxfPoint { X = p2.X, Y = p2.Y } }
                });
            }

            // Шаг 2: Для каждой элементарной линии строим эквидистантную линию внутрь контура
            // Сохраняем соответствие между индексами исходных точек и смещённых линий
            var offsetLines = new Dictionary<int, DxfPolyline>();
            
            for (int i = 0; i < elementaryLines.Count; i++)
            {
                var line = elementaryLines[i];
                if (line.Points.Count < 2)
                    continue;

                var p1 = line.Points[0];
                var p2 = line.Points[1];

                // Направление линии
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-9)
                    continue;

                // Нормаль к линии (перпендикуляр, повернутый на 90° против часовой стрелки)
                double nx = -dy / len;
                double ny = dx / len;

                // Направляем нормаль внутрь контура
                // Для контура по часовой стрелке нормаль "влево" смотрит наружу, инвертируем
                // Для отрицательного offset (смещение внутрь) нормаль должна быть направлена внутрь
                if (isClockwise)
                {
                    nx = -nx;
                    ny = -ny;
                }
                if (offset > 0) // Если offset положительный (наружу), инвертируем
                {
                    nx = -nx;
                    ny = -ny;
                }

                // Смещаем оба конца линии
                var offsetP1 = new DxfPoint { X = p1.X + nx * absOffset, Y = p1.Y + ny * absOffset };
                var offsetP2 = new DxfPoint { X = p2.X + nx * absOffset, Y = p2.Y + ny * absOffset };

                offsetLines[i] = new DxfPolyline
                {
                    Points = new List<DxfPoint> { offsetP1, offsetP2 }
                };
            }

            if (offsetLines.Count == 0)
                return null;

            // Шаг 3: Находим пересечения смещённых линий и строим контур из пересечений смещённых рёбер
            // Вместо разбиения всех линий и поиска замкнутых областей, строим контур напрямую из пересечений смещённых рёбер
            var offsetPoints = new List<DxfPoint>();
            
            for (int i = 0; i < contour.Points.Count; i++)
            {
                // Находим соответствующие смещённые линии
                int prevIdx = (i - 1 + contour.Points.Count) % contour.Points.Count;
                int currIdx = i;
                
                // Проверяем, что смещённые линии существуют
                if (!offsetLines.ContainsKey(prevIdx) || !offsetLines.ContainsKey(currIdx))
                    continue;
                
                var prevOffsetLine = offsetLines[prevIdx];
                var currOffsetLine = offsetLines[currIdx];
                
                if (prevOffsetLine.Points.Count >= 2 && currOffsetLine.Points.Count >= 2)
                {
                    // Находим пересечение двух смещённых линий
                    var intersection = SegmentSegmentIntersection(
                        prevOffsetLine.Points[0].X, prevOffsetLine.Points[0].Y,
                        prevOffsetLine.Points[prevOffsetLine.Points.Count - 1].X, prevOffsetLine.Points[prevOffsetLine.Points.Count - 1].Y,
                        currOffsetLine.Points[0].X, currOffsetLine.Points[0].Y,
                        currOffsetLine.Points[currOffsetLine.Points.Count - 1].X, currOffsetLine.Points[currOffsetLine.Points.Count - 1].Y);
                    
                    if (intersection != null)
                    {
                        offsetPoints.Add(intersection);
                    }
                    else
                    {
                        // Если пересечения нет (почти параллельные линии), берём точку на текущей смещённой линии
                        // Используем конец предыдущей линии или начало текущей
                        var p1 = prevOffsetLine.Points[prevOffsetLine.Points.Count - 1];
                        var p2 = currOffsetLine.Points[0];
                        // Берём середину между концами
                        offsetPoints.Add(new DxfPoint 
                        { 
                            X = (p1.X + p2.X) * 0.5, 
                            Y = (p1.Y + p2.Y) * 0.5 
                        });
                    }
                }
            }
            
            if (offsetPoints.Count >= 3)
            {
                // Замыкаем контур
                if (!PointsMatch(offsetPoints[0], offsetPoints[offsetPoints.Count - 1]))
                {
                    offsetPoints.Add(new DxfPoint { X = offsetPoints[0].X, Y = offsetPoints[0].Y });
                }
                return new DxfPolyline { Points = offsetPoints };
            }
            
            return null;
        }

        // Вспомогательные методы для работы с пересечениями и отсечением

        private List<DxfPolyline> SplitSegmentsAtIntersections(List<DxfPolyline> segments)
        {
            var splitSegments = new List<DxfPolyline>();
            var intersectionPoints = new Dictionary<int, List<(DxfPoint point, double distance)>>();

            // Находим все пересечения между сегментами
            for (int i = 0; i < segments.Count; i++)
            {
                var seg1 = segments[i];
                if (seg1.Points == null || seg1.Points.Count < 2)
                    continue;

                if (!intersectionPoints.ContainsKey(i))
                    intersectionPoints[i] = new List<(DxfPoint point, double distance)>();

                var p1Start = seg1.Points[0];
                var p1End = seg1.Points[seg1.Points.Count - 1];

                // Добавляем начальную и конечную точки сегмента с расстоянием 0 и длины сегмента
                double segLen = Math.Sqrt(Math.Pow(p1End.X - p1Start.X, 2) + Math.Pow(p1End.Y - p1Start.Y, 2));
                intersectionPoints[i].Add((new DxfPoint { X = p1Start.X, Y = p1Start.Y }, 0.0));
                intersectionPoints[i].Add((new DxfPoint { X = p1End.X, Y = p1End.Y }, segLen));

                for (int j = i + 1; j < segments.Count; j++)
                {
                    var seg2 = segments[j];
                    if (seg2.Points == null || seg2.Points.Count < 2)
                        continue;

                    // Находим пересечение между двумя отрезками
                    var intersection = SegmentSegmentIntersection(
                        p1Start.X, p1Start.Y, p1End.X, p1End.Y,
                        seg2.Points[0].X, seg2.Points[0].Y, seg2.Points[seg2.Points.Count - 1].X, seg2.Points[seg2.Points.Count - 1].Y);

                    if (intersection != null)
                    {
                        // Вычисляем расстояние от начала сегмента 1 до точки пересечения
                        double dx = intersection.X - p1Start.X;
                        double dy = intersection.Y - p1Start.Y;
                        double dist1 = Math.Sqrt(dx * dx + dy * dy);

                        // Вычисляем расстояние от начала сегмента 2 до точки пересечения
                        var p2Start = seg2.Points[0];
                        double dx2 = intersection.X - p2Start.X;
                        double dy2 = intersection.Y - p2Start.Y;
                        double dist2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

                        // Добавляем точку пересечения, если её ещё нет
                        if (!intersectionPoints[i].Any(item => PointsMatch(item.point, intersection)))
                            intersectionPoints[i].Add((intersection, dist1));

                        if (!intersectionPoints.ContainsKey(j))
                        {
                            var p2End = seg2.Points[seg2.Points.Count - 1];
                            double seg2Len = Math.Sqrt(Math.Pow(p2End.X - p2Start.X, 2) + Math.Pow(p2End.Y - p2Start.Y, 2));
                            intersectionPoints[j] = new List<(DxfPoint point, double distance)>();
                            intersectionPoints[j].Add((new DxfPoint { X = p2Start.X, Y = p2Start.Y }, 0.0));
                            intersectionPoints[j].Add((new DxfPoint { X = p2End.X, Y = p2End.Y }, seg2Len));
                        }
                        if (!intersectionPoints[j].Any(item => PointsMatch(item.point, intersection)))
                            intersectionPoints[j].Add((intersection, dist2));
                    }
                }
            }

            // Разбиваем сегменты в точках пересечения
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;

                if (intersectionPoints.ContainsKey(i) && intersectionPoints[i].Count >= 2)
                {
                    // Сортируем точки по расстоянию от начала сегмента
                    var sortedPoints = intersectionPoints[i].OrderBy(item => item.distance).ToList();

                    // Создаём подсегменты между соседними точками
                    for (int j = 0; j < sortedPoints.Count - 1; j++)
                    {
                        var p1 = sortedPoints[j].point;
                        var p2 = sortedPoints[j + 1].point;
                        if (!PointsMatch(p1, p2))
                        {
                            splitSegments.Add(new DxfPolyline
                            {
                                Points = new List<DxfPoint> { p1, p2 }
                            });
                        }
                    }
                }
                else
                {
                    // Сегмент без пересечений - добавляем как есть
                    splitSegments.Add(seg);
                }
            }

            return splitSegments;
        }

        private DxfPoint SegmentSegmentIntersection(double x1, double y1, double x2, double y2,
            double x3, double y3, double x4, double y4)
        {
            double dx1 = x2 - x1;
            double dy1 = y2 - y1;
            double dx2 = x4 - x3;
            double dy2 = y4 - y3;

            double denom = dx1 * dy2 - dy1 * dx2;
            if (Math.Abs(denom) < 1e-9)
                return null; // Параллельные линии

            double t1 = ((x3 - x1) * dy2 - (y3 - y1) * dx2) / denom;
            double t2 = ((x3 - x1) * dy1 - (y3 - y1) * dx1) / denom;

            const double tol = 1e-9;
            if (t1 < -tol || t1 > 1.0 + tol || t2 < -tol || t2 > 1.0 + tol)
                return null; // Пересечение вне отрезков

            return new DxfPoint
            {
                X = x1 + t1 * dx1,
                Y = y1 + t1 * dy1
            };
        }

        private bool PointsMatch(DxfPoint p1, DxfPoint p2)
        {
            if (p1 == null || p2 == null)
                return false;
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= 0.001; // Точность для сравнения точек
        }

        private DxfPoint FindLineContourIntersection(double x1, double y1, double x2, double y2, DxfPolyline contour)
        {
            DxfPoint closestIntersection = null;
            double minDist = double.MaxValue;

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];

                var intersection = SegmentSegmentIntersection(x1, y1, x2, y2, p1.X, p1.Y, p2.X, p2.Y);
                if (intersection != null)
                {
                    double dist = Math.Sqrt(Math.Pow(intersection.X - x1, 2) + Math.Pow(intersection.Y - y1, 2));
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestIntersection = intersection;
                    }
                }
            }

            return closestIntersection;
        }

        private List<DxfPoint> FindLineSegmentContourIntersections(double x1, double y1, double x2, double y2, DxfPolyline contour)
        {
            var intersections = new List<DxfPoint>();

            for (int i = 0; i < contour.Points.Count; i++)
            {
                var p1 = contour.Points[i];
                var p2 = contour.Points[(i + 1) % contour.Points.Count];

                var intersection = SegmentSegmentIntersection(x1, y1, x2, y2, p1.X, p1.Y, p2.X, p2.Y);
                if (intersection != null)
                {
                    // Проверяем, нет ли уже такой точки
                    if (!intersections.Any(p => PointsMatch(p, intersection)))
                    {
                        intersections.Add(intersection);
                    }
                }
            }

            return intersections;
        }

        private List<DxfPolyline> FindClosedAreasFromIntersections(List<DxfPolyline> segments)
        {
            var contours = new List<DxfPolyline>();

            if (segments == null || segments.Count == 0)
                return contours;

            // Строим граф соединений на основе точек
            var pointGraph = BuildPointGraph(segments);

            if (pointGraph == null || pointGraph.Count == 0)
                return contours;

            // Ищем все циклы в графе точек
            var cycles = FindCyclesInPointGraph(pointGraph);

            // Фильтруем циклы - оставляем только те, которые образуют замкнутые области
            foreach (var cycle in cycles)
            {
                if (cycle != null && cycle.Count >= 3)
                {
                    var contour = BuildContourFromPointCycle(cycle);
                    if (contour != null && IsClosedContour(contour))
                    {
                        var area = GetContourArea(contour);
                        if (area > 0.001 * 0.001) // Минимальная площадь
                        {
                            contours.Add(contour);
                        }
                    }
                }
            }

            return contours;
        }

        private Dictionary<DxfPoint, List<DxfPoint>> BuildPointGraph(List<DxfPolyline> segments)
        {
            var graph = new Dictionary<DxfPoint, List<DxfPoint>>();

            foreach (var seg in segments)
            {
                if (seg.Points == null || seg.Points.Count < 2)
                    continue;

                var p1 = seg.Points[0];
                var p2 = seg.Points[seg.Points.Count - 1];

                // Нормализуем точки (используем существующие, если они близки)
                var normalizedP1 = FindOrAddPoint(graph, p1);
                var normalizedP2 = FindOrAddPoint(graph, p2);

                if (!graph[normalizedP1].Contains(normalizedP2))
                    graph[normalizedP1].Add(normalizedP2);
                if (!graph[normalizedP2].Contains(normalizedP1))
                    graph[normalizedP2].Add(normalizedP1);
            }

            return graph;
        }

        private DxfPoint FindOrAddPoint(Dictionary<DxfPoint, List<DxfPoint>> graph, DxfPoint point)
        {
            foreach (var key in graph.Keys)
            {
                if (PointsMatch(key, point))
                    return key;
            }

            graph[point] = new List<DxfPoint>();
            return point;
        }

        private List<List<DxfPoint>> FindCyclesInPointGraph(Dictionary<DxfPoint, List<DxfPoint>> graph)
        {
            var cycles = new List<List<DxfPoint>>();
            var visited = new HashSet<DxfPoint>();

            foreach (var startPoint in graph.Keys)
            {
                if (visited.Contains(startPoint))
                    continue;

                FindCyclesDFS(graph, startPoint, startPoint, new List<DxfPoint> { startPoint }, cycles, visited, new HashSet<string>());
            }

            return cycles;
        }

        private void FindCyclesDFS(Dictionary<DxfPoint, List<DxfPoint>> graph, DxfPoint start, DxfPoint current,
            List<DxfPoint> path, List<List<DxfPoint>> cycles, HashSet<DxfPoint> visited, HashSet<string> foundCycles)
        {
            if (path.Count > 1 && PointsMatch(current, start))
            {
                // Найден цикл
                var cycleKey = string.Join("|", path.Select(p => $"{p.X:F6},{p.Y:F6}"));
                if (!foundCycles.Contains(cycleKey) && path.Count >= 3)
                {
                    cycles.Add(new List<DxfPoint>(path));
                    foundCycles.Add(cycleKey);
                }
                return;
            }

            if (path.Count > graph.Count)
                return; // Защита от бесконечной рекурсии

            if (!graph.ContainsKey(current))
                return;

            foreach (var neighbor in graph[current])
            {
                if (path.Count > 1 && PointsMatch(neighbor, start))
                {
                    // Замыкаем цикл
                    var cycle = new List<DxfPoint>(path) { neighbor };
                    var cycleKey = string.Join("|", cycle.Select(p => $"{p.X:F6},{p.Y:F6}"));
                    if (!foundCycles.Contains(cycleKey) && cycle.Count >= 3)
                    {
                        cycles.Add(cycle);
                        foundCycles.Add(cycleKey);
                    }
                }
                else if (!path.Skip(1).Any(p => PointsMatch(p, neighbor)))
                {
                    // Не посещали эту точку в текущем пути
                    FindCyclesDFS(graph, start, neighbor, new List<DxfPoint>(path) { neighbor }, cycles, visited, foundCycles);
                }
            }

            visited.Add(current);
        }

        private DxfPolyline BuildContourFromPointCycle(List<DxfPoint> cycle)
        {
            if (cycle == null || cycle.Count < 3)
                return null;

            var contourPoints = new List<DxfPoint>(cycle);
            // Замыкаем контур, если первая и последняя точки не совпадают
            if (!PointsMatch(contourPoints[0], contourPoints[contourPoints.Count - 1]))
            {
                contourPoints.Add(new DxfPoint { X = contourPoints[0].X, Y = contourPoints[0].Y });
            }

            return new DxfPolyline { Points = contourPoints };
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

        private PocketDxfOperation CloneOp(PocketDxfOperation src)
        {
            return new PocketDxfOperation
            {
                Name = src.Name,
                IsEnabled = src.IsEnabled,
                ClosedContours = src.ClosedContours,
                DxfFilePath = src.DxfFilePath,
                Direction = src.Direction,
                PocketStrategy = src.PocketStrategy,
                TotalDepth = src.TotalDepth,
                StepDepth = src.StepDepth,
                ToolDiameter = src.ToolDiameter,
                ContourHeight = src.ContourHeight,
                FeedXYRapid = src.FeedXYRapid,
                FeedXYWork = src.FeedXYWork,
                FeedZRapid = src.FeedZRapid,
                FeedZWork = src.FeedZWork,
                SafeZHeight = src.SafeZHeight,
                RetractHeight = src.RetractHeight,
                StepPercentOfTool = src.StepPercentOfTool,
                Decimals = src.Decimals,
                LineAngleDeg = src.LineAngleDeg,
                WallTaperAngleDeg = src.WallTaperAngleDeg,
                IsRoughingEnabled = src.IsRoughingEnabled,
                IsFinishingEnabled = src.IsFinishingEnabled,
                FinishAllowance = src.FinishAllowance,
                FinishingMode = src.FinishingMode
            };
        }
    }
}

