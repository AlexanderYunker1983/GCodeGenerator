using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketRectangleOperationGenerator : IOperationGenerator
    {
        public void Generate(OperationBase operation,
                             Action<string> addLine,
                             string g0,          // команда «переход» (обычно G0)
                             string g1,          // команда «работа»   (обычно G1)
                             GCodeSettings settings)
        {
            var op = operation as PocketRectangleOperation;
            if (op == null) return;

            // Определяем режимы: черновая / чистовая / обычная
            bool roughing = op.IsRoughingEnabled;
            bool finishing = op.IsFinishingEnabled;
            double allowance = Math.Max(0.0, op.FinishAllowance);

            // Если оба чекбокса выключены – работаем как раньше (полная обработка без припуска)
            if (!roughing && !finishing)
            {
                roughing = true;
                allowance = 0.0;
            }

            // Черновая обработка: уменьшаем глубину и габариты на припуск
            if (roughing)
            {
                var roughOp = CloneOp(op);
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, roughOp.TotalDepth - 1e-6));

                if (depthAllowance > 0)
                {
                    // Обрезаем по глубине
                    roughOp.TotalDepth -= depthAllowance;

                    // Обрезаем по контуру: оставляем припуск по стенке
                    roughOp.Width -= 2 * depthAllowance;
                    roughOp.Height -= 2 * depthAllowance;

                    if (roughOp.Width <= 0 || roughOp.Height <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Pocket too small after roughing allowance, skipping)");
                        return;
                    }
                }

                GenerateInternal(roughOp, addLine, g0, g1, settings);
            }

            // Чистовая обработка: обрабатываем только припуск по дну и/или стенкам
            if (finishing && allowance > 0)
            {
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, op.TotalDepth));
                if (depthAllowance < 1e-6)
                    return;

                // Базовая чистовая операция по глубине: работаем только в слое припуска
                var baseFinishOp = CloneOp(op);
                baseFinishOp.ContourHeight = op.ContourHeight - (op.TotalDepth - depthAllowance);
                baseFinishOp.TotalDepth = depthAllowance;
                baseFinishOp.IsRoughingEnabled = false;
                baseFinishOp.IsFinishingEnabled = false;
                baseFinishOp.FinishAllowance = allowance; // сохраняем припуск для логики стенок

                switch (op.FinishingMode)
                {
                    case PocketFinishingMode.Walls:
                        // Обрабатываем только стенки, дно не трогаем
                        GenerateWallsFinishing(baseFinishOp, allowance, addLine, g0, g1, settings);
                        break;

                    case PocketFinishingMode.Bottom:
                        {
                            // Обрабатываем только дно: внутренняя область без стенок
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.Width -= 2 * allowance;
                            bottomOp.Height -= 2 * allowance;
                            if (bottomOp.Width > 0 && bottomOp.Height > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);
                        }
                        break;

                    case PocketFinishingMode.All:
                    default:
                        {
                            // Сначала дно (внутри), затем стенки
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.Width -= 2 * allowance;
                            bottomOp.Height -= 2 * allowance;
                            if (bottomOp.Width > 0 && bottomOp.Height > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);

                            GenerateWallsFinishing(baseFinishOp, allowance, addLine, g0, g1, settings);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Внутренняя реализация генерации для одной операции (без учёта черновой/чистовой логики).
        /// </summary>
        private void GenerateInternal(PocketRectangleOperation op,
                                      Action<string> addLine,
                                      string g0,
                                      string g1,
                                      GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // ----------- общие параметры фрезы ----------
            double toolRadius = op.ToolDiameter / 2.0;
            double step = op.ToolDiameter * (op.StepPercentOfTool <= 0
                                                    ? 0.4
                                                    : op.StepPercentOfTool / 100.0);
            if (step < 1e-6) step = op.ToolDiameter * 0.4;

            // ----------- центр и размеры прямоугольника ----------
            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY,
                      op.Width, op.Height, out double cx, out double cy);

            double baseHalfW = op.Width / 2.0;
            double baseHalfH = op.Height / 2.0;
            if (baseHalfW <= toolRadius || baseHalfH <= toolRadius) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            // ----------- ориентация ----------
            var angleRad = op.RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            // Функция поворота координат относительно центра
            (double X, double Y) Rot(double x, double y)
                => (cx + x * cos - y * sin,
                    cy + x * sin + y * cos);

            // ----------- глубина резки ----------
            var currentZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;
            var pass = 0;

            while (currentZ > finalZ)
            {
                var nextZ = currentZ - op.StepDepth;
                if (nextZ < finalZ) nextZ = finalZ;
                pass++;

                if (settings.UseComments)
                    addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                // Переходы в безопасную высоту и центр
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                // Понижение и начало резки
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                // ----------- уклон стенки ----------
                var depthFromTop = op.ContourHeight - nextZ;
                var offset = depthFromTop * taperTan;
                var effectiveToolRadius = toolRadius + offset;

                var halfW = baseHalfW - effectiveToolRadius;
                var halfH = baseHalfH - effectiveToolRadius;
                if (halfW <= 0 || halfH <= 0)
                {
                    if (settings.UseComments)
                        addLine("(Taper offset too large, stopping)");
                    break;
                }

                // ----------- генерация траектории ----------
                if (op.PocketStrategy == PocketStrategy.Spiral)
                    GenerateSpiral(addLine, g0, g1,
                                   fmt, culture,
                                   cx, cy, halfW, halfH,
                                   step, op.Direction, angleRad,
                                   op.FeedXYWork,
                                   op.SafeZHeight, nextZ,
                                   op.FeedZRapid, op.FeedZWork);
                else if (op.PocketStrategy == PocketStrategy.Radial)
                {
                    var lastHit = GenerateRadial(addLine, g1,
                                   fmt, culture,
                                   cx, cy, halfW, halfH,
                                   step, angleRad,
                                   op.Direction,
                                   op.FeedXYWork);
                    // Завершающий полный проход по контуру с учётом последней точки
                    GenerateConcentricRectangles(addLine, g1,
                                                fmt, culture,
                                                cx, cy, halfW, halfH,
                                                op.Direction,
                                                step, angleRad,
                                                op.FeedXYWork,
                                                onlyOuter: true,
                                                startPoint: new Point(lastHit.x, lastHit.y));
                }
                else if (op.PocketStrategy == PocketStrategy.Lines)
                {
                    var lastHit = GenerateLines(addLine, g0, g1,
                                   fmt, culture,
                                   cx, cy, halfW, halfH,
                                   step, angleRad,
                                   op.Direction,
                                   op.LineAngleDeg,
                                   op.FeedXYRapid,
                                   op.FeedXYWork,
                                   nextZ,
                                   op.SafeZHeight,
                                   op.FeedZRapid);

                    GenerateConcentricRectangles(addLine, g1,
                                                fmt, culture,
                                                cx, cy, halfW, halfH,
                                                op.Direction,
                                                step, angleRad,
                                                op.FeedXYWork,
                                                onlyOuter: true,
                                                startPoint: new Point(lastHit.x, lastHit.y));
                }
                else if (op.PocketStrategy == PocketStrategy.ZigZag)
                {
                    var segments = GenerateLineSegments(cx, cy, halfW, halfH, step, angleRad, op.LineAngleDeg);
                    if (segments.Count > 0)
                    {
                        // Начало — первая линия, в прямом направлении
                        addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                        addLine($"{g0} X{segments[0].start.X.ToString(fmt, culture)} Y{segments[0].start.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                        addLine($"{g0} Z{nextZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                        for (int i = 0; i < segments.Count; i++)
                        {
                            var seg = segments[i];
                            bool reverse = (i % 2 == 1);
                            var start = reverse ? seg.end : seg.start;
                            var end = reverse ? seg.start : seg.end;

                            addLine($"{g1} X{end.X.ToString(fmt, culture)} Y{end.Y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                            if (i + 1 < segments.Count)
                            {
                                var nextSeg = segments[i + 1];
                                var nextStart = (i % 2 == 0) ? nextSeg.end : nextSeg.start; // следующий старт по контуру, чтобы развернуться
                                TravelAlongRectangle(addLine, g1, fmt, culture, end, nextStart, cx, cy, halfW, halfH, op.Direction, op.FeedXYWork);
                            }
                        }

                        var last = segments[segments.Count - 1];
                        var lastEnd = (segments.Count % 2 == 0) ? last.end : last.start;
                        GenerateConcentricRectangles(addLine, g1,
                                                    fmt, culture,
                                                    cx, cy, halfW, halfH,
                                                    op.Direction,
                                                    step, angleRad,
                                                    op.FeedXYWork,
                                                    onlyOuter: true,
                                                    startPoint: lastEnd);
                    }
                }
                else
                    GenerateConcentricRectangles(addLine, g1,
                                                fmt, culture,
                                                cx, cy, halfW, halfH,
                                                op.Direction,
                                                step, angleRad,
                                                op.FeedXYWork);

                // В конце прохода слоя не поднимаем фрезу прямо на контуре:
                // сначала уходим в центр кармана, затем поднимаемся.
                addLine($"{g1} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                // Переход к безопасной высоте
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                currentZ = nextZ;
            }
        }

        /// <summary>
        /// Чистовая обработка только стенок (outer contour) на заданном слое по глубине.
        /// Центральная часть кармана не перерабатывается.
        /// </summary>
        private void GenerateWallsFinishing(PocketRectangleOperation op,
                                            double radialAllowance,
                                            Action<string> addLine,
                                            string g0,
                                            string g1,
                                            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepRadial = op.StepDepth; // для стенок «глубина за проход» – это шаг по припуску (радиально)
            if (stepRadial <= 0)
                stepRadial = op.ToolDiameter * 0.25;

            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY,
                      op.Width, op.Height, out double cx, out double cy);

            double baseHalfW = op.Width / 2.0;
            double baseHalfH = op.Height / 2.0;
            if (baseHalfW <= toolRadius || baseHalfH <= toolRadius) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            var angleRad = op.RotationAngle * Math.PI / 180.0;

            // Для стенок чистовая идёт на всю высоту припуска за один раз по Z,
            // а «глубина за проход» определяет количество радиальных проходов по припуску.
            var startZ = op.ContourHeight;
            var finalZ = op.ContourHeight - op.TotalDepth;

            double allowance = Math.Max(0.0, radialAllowance);
            int radialPasses = allowance > 1e-6
                ? Math.Max(1, (int)Math.Ceiling(allowance / stepRadial))
                : 1;
            double radialStep = (radialPasses > 0 && allowance > 1e-6) ? allowance / radialPasses : 0.0;

            // Учитываем уклон стенки на нижней точке
            var depthFromTop = op.ContourHeight - finalZ;
            var offset = depthFromTop * taperTan;
            var effectiveToolRadius = toolRadius + offset;

            var finalHalfW = baseHalfW - effectiveToolRadius;
            var finalHalfH = baseHalfH - effectiveToolRadius;
            if (finalHalfW <= 0 || finalHalfH <= 0)
            {
                if (settings.UseComments)
                    addLine("(Taper offset too large, stopping finishing walls)");
                return;
            }

            for (int i = 0; i < radialPasses; i++)
            {
                double remaining = allowance - (i + 1) * radialStep;
                if (remaining < 0) remaining = 0;

                var halfW = finalHalfW - remaining;
                var halfH = finalHalfH - remaining;
                if (halfW <= 0 || halfH <= 0)
                    continue;

                if (settings.UseComments)
                    addLine($"(Finishing walls radial pass {i + 1}/{radialPasses}, stock {remaining.ToString(fmt, culture)}mm)");

                var startCorner = new Point(cx - halfW, cy - halfH);

                // Подъём, подход к углу и один проход по Z на всю высоту припуска
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startCorner.X.ToString(fmt, culture)} Y{startCorner.Y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{startZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{finalZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                // Обходим только внешний прямоугольный контур на этой радиальной позиции
                GenerateConcentricRectangles(addLine, g1,
                                            fmt, culture,
                                            cx, cy, halfW, halfH,
                                            op.Direction,
                                            stepRadial, angleRad,
                                            op.FeedXYWork,
                                            onlyOuter: true,
                                            startPoint: startCorner);

                // После обхода уходим в центр и поднимаем фрезу
                addLine($"{g1} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            }
        }

        private PocketRectangleOperation CloneOp(PocketRectangleOperation src)
        {
            return new PocketRectangleOperation
            {
                Name = src.Name,
                IsEnabled = src.IsEnabled,
                Direction = src.Direction,
                PocketStrategy = src.PocketStrategy,
                Width = src.Width,
                Height = src.Height,
                RotationAngle = src.RotationAngle,
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
                ReferencePointX = src.ReferencePointX,
                ReferencePointY = src.ReferencePointY,
                ReferencePointType = src.ReferencePointType,
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

        #region Вспомогательные методы

        /// <summary>
        /// Генерация траектории Archimedean‑спирали,
        /// ограниченной прямоугольником (halfW × halfH).
        /// При выходе спирали за пределы прямоугольника
        /// движение продолжается по контуру до точки входа спирали обратно в контур.
        /// После последней точки спирали выполняется полный обход внешнего прямоугольника.
        /// </summary>
        private void GenerateSpiral(Action<string> addLine, string g0, string g1,
                                    string fmt, CultureInfo culture,
                                    double cx, double cy,
                                    double halfW, double halfH,
                                    double step,
                                    MillingDirection direction,
                                    double angleRad,
                                    double feedXYWork,
                                    double safeZ, double currentZ,
                                    double feedZRapid, double feedZWork)
        {
            // Максимальный радиус спирали – минимум от половин ширины/высоты
            var maxRadius = Math.Sqrt(halfW * halfW + halfH * halfH);

            const double a = 0.0;                        // r(θ) = a + b·θ
            double b = step / (2 * Math.PI);             // радиальная скорость за один оборот

            int pointsPerRevolution = 128;
            double angleStep = 2 * Math.PI / pointsPerRevolution;
            double dirSign = direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            double θMax = (maxRadius - a) / b;

            // Границы прямоугольника
            double left = cx - halfW;
            double right = cx + halfW;
            double bottom = cy - halfH;
            double top = cy + halfH;

            // Функция проверки, находится ли точка внутри прямоугольника (строго внутри, не на границе)
            bool IsInside(double x, double y)
            {
                double tolerance = 1e-6;
                return x > left + tolerance && x < right - tolerance && y > bottom + tolerance && y < top - tolerance;
            }
            
            // Функция проверки, находится ли точка на границе или внутри
            bool IsOnOrInside(double x, double y)
            {
                return x >= left && x <= right && y >= bottom && y <= top;
            }

            // Функция нахождения точки пересечения отрезка с границей прямоугольника
            bool FindIntersection(double x1, double y1, double x2, double y2,
                                 out double ix, out double iy)
            {
                ix = x2;
                iy = y2;
                double dx = x2 - x1;
                double dy = y2 - y1;
                if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return false;

                double tMin = 0.0;
                double tMax = 1.0;
                bool found = false;

                if (Math.Abs(dx) > 1e-9)
                {
                    double tLeft = (left - x1) / dx;
                    double tRight = (right - x1) / dx;
                    if (tLeft > tRight) { double tmp = tLeft; tLeft = tRight; tRight = tmp; }
                    if (tLeft >= 0 && tLeft <= 1 && tLeft > tMin) { tMin = tLeft; found = true; }
                    if (tRight >= 0 && tRight <= 1 && tRight < tMax) { tMax = tRight; found = true; }
                }

                if (Math.Abs(dy) > 1e-9)
                {
                    double tBottom = (bottom - y1) / dy;
                    double tTop = (top - y1) / dy;
                    if (tBottom > tTop) { double tmp = tBottom; tBottom = tTop; tTop = tmp; }
                    if (tBottom >= 0 && tBottom <= 1 && tBottom > tMin) { tMin = tBottom; found = true; }
                    if (tTop >= 0 && tTop <= 1 && tTop < tMax) { tMax = tTop; found = true; }
                }

                if (!found || tMin > tMax || tMin < 0 || tMin > 1) return false;

                ix = x1 + tMin * dx;
                iy = y1 + tMin * dy;
                return true;
            }

            // Функция движения по контуру от точки (x1, y1) до точки (x2, y2)
            // Проходит через все углы между этими точками (по кратчайшему пути)
            // Возвращает true, если путь построен успешно; false - если нужен подъём инструмента
            bool MoveAlongContour(double x1, double y1, double x2, double y2)
            {
                double tolerance = 1e-4;
                
                // Углы прямоугольника (против часовой стрелки)
                var corners = new[]
                {
                    (left, bottom),   // 0: левый нижний
                    (right, bottom),  // 1: правый нижний
                    (right, top),     // 2: правый верхний
                    (left, top),      // 3: левый верхний
                };
                
                // Определяем, на какой стороне находится точка (0=низ, 1=право, 2=верх, 3=лево)
                int GetSide(double px, double py)
                {
                    if (Math.Abs(py - bottom) < tolerance) return 0; // нижняя
                    if (Math.Abs(px - right) < tolerance) return 1; // правая
                    if (Math.Abs(py - top) < tolerance) return 2; // верхняя
                    if (Math.Abs(px - left) < tolerance) return 3; // левая
                    return -1;
                }

                int sideStart = GetSide(x1, y1);
                int sideEnd = GetSide(x2, y2);
                
                if (sideStart < 0 || sideEnd < 0) return false;
                if (sideStart == sideEnd)
                {
                    // На одной стороне - просто идём к конечной точке
                    addLine($"{g1} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    return true;
                }

                // Вычисляем расстояние в обоих направлениях
                double DistCCW()
                {
                    double dist = 0;
                    double px = x1, py = y1;
                    int side = sideStart;
                    while (side != sideEnd)
                    {
                        int cornerIdx = (side + 1) % 4;
                        dist += Math.Sqrt(Math.Pow(px - corners[cornerIdx].Item1, 2) + Math.Pow(py - corners[cornerIdx].Item2, 2));
                        px = corners[cornerIdx].Item1;
                        py = corners[cornerIdx].Item2;
                        side = (side + 1) % 4;
                    }
                    dist += Math.Sqrt(Math.Pow(px - x2, 2) + Math.Pow(py - y2, 2));
                    return dist;
                }
                
                double DistCW()
                {
                    double dist = 0;
                    double px = x1, py = y1;
                    int side = sideStart;
                    while (side != sideEnd)
                    {
                        int cornerIdx = side;
                        dist += Math.Sqrt(Math.Pow(px - corners[cornerIdx].Item1, 2) + Math.Pow(py - corners[cornerIdx].Item2, 2));
                        px = corners[cornerIdx].Item1;
                        py = corners[cornerIdx].Item2;
                        side = (side + 3) % 4;
                    }
                    dist += Math.Sqrt(Math.Pow(px - x2, 2) + Math.Pow(py - y2, 2));
                    return dist;
                }

                bool ccw = DistCCW() <= DistCW();
                
                int currentSide = sideStart;
                while (currentSide != sideEnd)
                {
                    int cornerIdx = ccw ? (currentSide + 1) % 4 : currentSide;
                    addLine($"{g1} X{corners[cornerIdx].Item1.ToString(fmt, culture)} Y{corners[cornerIdx].Item2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    currentSide = ccw ? (currentSide + 1) % 4 : (currentSide + 3) % 4;
                }
                
                addLine($"{g1} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                return true;
            }
            
            // Функция для перемещения с подъёмом инструмента
            void MoveWithRetract(double x1, double y1, double x2, double y2)
            {
                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            }

            // Сначала собираем все точки спирали
            var spiralPoints = new System.Collections.Generic.List<(double x, double y)>();
            spiralPoints.Add((cx, cy));
            
            for (double θ = angleStep; θ <= θMax + 1e-9; θ += angleStep)
            {
                double r = a + b * θ;
                double ang = θ * dirSign;
                double xSpiral = cx + r * Math.Cos(ang);
                double ySpiral = cy + r * Math.Sin(ang);
                spiralPoints.Add((xSpiral, ySpiral));
            }

            // Функция clamp - ограничивает точку контуром
            (double x, double y) Clamp(double x, double y)
            {
                return (Math.Max(left, Math.Min(right, x)), Math.Max(bottom, Math.Min(top, y)));
            }

            // Функция для нахождения точки пересечения отрезка с границей прямоугольника
            // Находит ВСЕ пересечения и возвращает ближайшее к (x1, y1)
            (double x, double y) FindBorderIntersection(double x1, double y1, double x2, double y2)
            {
                double dx = x2 - x1;
                double dy = y2 - y1;
                
                var intersections = new System.Collections.Generic.List<(double t, double x, double y)>();

                // Пересечение с левой стороной (x = left)
                if (Math.Abs(dx) > 1e-9)
                {
                    double t = (left - x1) / dx;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double yInt = y1 + t * dy;
                        if (yInt >= bottom - 1e-9 && yInt <= top + 1e-9)
                            intersections.Add((t, left, Math.Max(bottom, Math.Min(top, yInt))));
                    }
                }
                
                // Пересечение с правой стороной (x = right)
                if (Math.Abs(dx) > 1e-9)
                {
                    double t = (right - x1) / dx;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double yInt = y1 + t * dy;
                        if (yInt >= bottom - 1e-9 && yInt <= top + 1e-9)
                            intersections.Add((t, right, Math.Max(bottom, Math.Min(top, yInt))));
                    }
                }
                
                // Пересечение с нижней стороной (y = bottom)
                if (Math.Abs(dy) > 1e-9)
                {
                    double t = (bottom - y1) / dy;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double xInt = x1 + t * dx;
                        if (xInt >= left - 1e-9 && xInt <= right + 1e-9)
                            intersections.Add((t, Math.Max(left, Math.Min(right, xInt)), bottom));
                    }
                }
                
                // Пересечение с верхней стороной (y = top)
                if (Math.Abs(dy) > 1e-9)
                {
                    double t = (top - y1) / dy;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double xInt = x1 + t * dx;
                        if (xInt >= left - 1e-9 && xInt <= right + 1e-9)
                            intersections.Add((t, Math.Max(left, Math.Min(right, xInt)), top));
                    }
                }

                if (intersections.Count == 0)
                    return Clamp(x2, y2);

                // Находим ближайшее пересечение
                intersections.Sort((p1, p2) => p1.t.CompareTo(p2.t));
                return (intersections[0].x, intersections[0].y);
            }

            // Теперь обрабатываем точки спирали
            double prevX = cx, prevY = cy;
            bool prevInside = true;
            addLine($"{g1} X{prevX.ToString(fmt, culture)} Y{prevY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

            double exitX = 0, exitY = 0;
            bool hasExitPoint = false;

            for (int i = 1; i < spiralPoints.Count; i++)
            {
                var point = spiralPoints[i];
                double xSpiral = point.x;
                double ySpiral = point.y;
                bool currentInside = IsOnOrInside(xSpiral, ySpiral);

                if (prevInside && currentInside)
                {
                    // Обе точки внутри - просто добавляем
                    addLine($"{g1} X{xSpiral.ToString(fmt, culture)} Y{ySpiral.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    prevX = xSpiral;
                    prevY = ySpiral;
                }
                else if (prevInside && !currentInside)
                {
                    // Выход из контура - находим точку выхода и запоминаем
                    var exit = FindBorderIntersection(prevX, prevY, xSpiral, ySpiral);
                    exitX = exit.x;
                    exitY = exit.y;
                    addLine($"{g1} X{exitX.ToString(fmt, culture)} Y{exitY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    hasExitPoint = true;
                    prevInside = false;
                    prevX = xSpiral;
                    prevY = ySpiral;
                }
                else if (!prevInside && currentInside)
                {
                    // Вход в контур - находим точку входа
                    var prevPoint = spiralPoints[i - 1];
                    var entry = FindBorderIntersection(prevPoint.x, prevPoint.y, xSpiral, ySpiral);
                    
                    if (hasExitPoint)
                    {
                        // Строим путь по контуру от точки выхода до точки входа
                        // Если не удалось - поднимаем инструмент
                        if (!MoveAlongContour(exitX, exitY, entry.x, entry.y))
                        {
                            MoveWithRetract(exitX, exitY, entry.x, entry.y);
                        }
                        hasExitPoint = false;
                    }
                    
                    addLine($"{g1} X{xSpiral.ToString(fmt, culture)} Y{ySpiral.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    prevX = xSpiral;
                    prevY = ySpiral;
                    prevInside = true;
                }
                else
                {
                    // Обе точки снаружи - обновляем предыдущую точку, но не добавляем в траекторию
                    prevX = xSpiral;
                    prevY = ySpiral;
                    prevInside = false;
                }
            }

            // ----------- полный обход внешнего прямоугольника -------------
            // От последней точки (точки выхода или последней точки внутри) делаем полный обход
            double startX = hasExitPoint ? exitX : prevX;
            double startY = hasExitPoint ? exitY : prevY;

            // Определяем, на какой стороне находится текущая точка (0=низ, 1=право, 2=верх, 3=лево)
            double eps = 1e-4;
            int GetSideSimple(double x, double y)
            {
                if (Math.Abs(y - bottom) < eps) return 0; // нижняя
                if (Math.Abs(x - right) < eps) return 1; // правая
                if (Math.Abs(y - top) < eps) return 2; // верхняя
                if (Math.Abs(x - left) < eps) return 3; // левая
                return -1;
            }

            // Углы прямоугольника (против часовой стрелки, начиная с левого нижнего)
            // Угол i находится между сторонами (i-1) и i
            var rectCorners = new[]
            {
                (left, bottom),   // 0: между сторонами 3 (лево) и 0 (низ)
                (right, bottom),  // 1: между сторонами 0 (низ) и 1 (право)
                (right, top),     // 2: между сторонами 1 (право) и 2 (верх)
                (left, top),      // 3: между сторонами 2 (верх) и 3 (лево)
            };

            int startSide = GetSideSimple(startX, startY);
            
            // Полный обход контура от текущей точки против часовой стрелки
            if (startSide >= 0)
            {
                // Сначала идём к ближайшему углу на текущей стороне (против часовой стрелки)
                // Угол (startSide + 1) % 4 - это угол в конце текущей стороны (против часовой)
                int firstCorner = (startSide + 1) % 4;
                addLine($"{g1} X{rectCorners[firstCorner].Item1.ToString(fmt, culture)} Y{rectCorners[firstCorner].Item2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                
                // Проходим оставшиеся 3 угла
                for (int i = 1; i <= 3; i++)
                {
                    int cornerIdx = (firstCorner + i) % 4;
                    addLine($"{g1} X{rectCorners[cornerIdx].Item1.ToString(fmt, culture)} Y{rectCorners[cornerIdx].Item2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
                
                // Возвращаемся к начальной точке
                addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
            else
            {
                // Если не на контуре - поднимаем инструмент и делаем полный обход
                MoveWithRetract(startX, startY, left, bottom);
                addLine($"{g1} X{right.ToString(fmt, culture)} Y{bottom.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{right.ToString(fmt, culture)} Y{top.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{left.ToString(fmt, culture)} Y{top.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{left.ToString(fmt, culture)} Y{bottom.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
        }


        /// <summary>
        /// Радиальные линии для прямоугольного кармана.
        /// Центр → граница, шаг по контуру, возврат в центр.
        /// </summary>
        private (double x, double y) GenerateRadial(Action<string> addLine,
                                    string g1,
                                    string fmt, CultureInfo culture,
                                    double cx, double cy,
                                    double halfW, double halfH,
                                    double step,
                                    double angleRad,
                                    MillingDirection direction,
                                    double feedXYWork)
        {
            double perimeter = 4 * (halfW + halfH);
            int segments = Math.Max(16, (int)Math.Ceiling(perimeter / step));
            double angleStep = 2 * Math.PI / segments * ((direction == MillingDirection.Clockwise) ? -1 : 1);

            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            (double lx, double ly) ToLocal(double x, double y)
                => ((x - cx) * cos + (y - cy) * sin,
                    -(x - cx) * sin + (y - cy) * cos);

            (double wx, double wy) ToWorld(double x, double y)
                => (cx + x * cos - y * sin,
                    cy + x * sin + y * cos);

            double ParamOnPerimeter(double x, double y)
            {
                double eps = 1e-6;
                if (Math.Abs(x - halfW) < eps) // right, bottom->top
                    return (y + halfH);
                if (Math.Abs(y - halfH) < eps) // top, right->left
                    return 2 * halfH + (halfW - x);
                if (Math.Abs(x + halfW) < eps) // left, top->bottom
                    return 2 * halfH + 2 * halfW + (halfH - y);
                // bottom, left->right
                return 2 * (halfH + halfW) + 2 * halfH + (x + halfW);
            }

            (double x, double y) PointFromParam(double s)
            {
                double per = 4 * (halfW + halfH);
                s = (s % per + per) % per;
                if (s <= 2 * halfH) // right
                    return (halfW, -halfH + s);
                s -= 2 * halfH;
                if (s <= 2 * halfW) // top
                    return (halfW - s, halfH);
                s -= 2 * halfW;
                if (s <= 2 * halfH) // left
                    return (-halfW, halfH - s);
                s -= 2 * halfH; // bottom
                return (-halfW + s, -halfH);
            }

            (double x, double y) RayIntersection(double dx, double dy)
            {
                double t = double.PositiveInfinity;
                if (Math.Abs(dx) > 1e-9)
                {
                    double tx = dx > 0 ? halfW / dx : -halfW / dx;
                    if (tx > 0) t = Math.Min(t, tx);
                }
                if (Math.Abs(dy) > 1e-9)
                {
                    double ty = dy > 0 ? halfH / dy : -halfH / dy;
                    if (ty > 0) t = Math.Min(t, ty);
                }
                if (double.IsInfinity(t)) t = 0;
                return (dx * t, dy * t);
            }

            double dirSign = direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            (double x, double y) lastHitWorld = (cx + halfW * cos - (-halfH) * sin, cy + halfW * sin + (-halfH) * cos); // fallback

            for (int i = 0; i < segments; i++)
            {
                double angW = angleStep * i;
                double dxW = Math.Cos(angW);
                double dyW = Math.Sin(angW);

                // В локальные координаты прямоугольника
                double dxL = dxW * cos + dyW * sin;
                double dyL = -dxW * sin + dyW * cos;

                var hitLocal = RayIntersection(dxL, dyL);
                var hitWorld = ToWorld(hitLocal.x, hitLocal.y);

                double s0 = ParamOnPerimeter(hitLocal.x, hitLocal.y);
                double s1 = s0 + dirSign * step;
                var p2Local = PointFromParam(s1);
                var p2World = ToWorld(p2Local.x, p2Local.y);

                addLine($"{g1} X{hitWorld.wx.ToString(fmt, culture)} Y{hitWorld.wy.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{p2World.wx.ToString(fmt, culture)} Y{p2World.wy.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                lastHitWorld = p2World;
            }

            return lastHitWorld;
        }

        private System.Collections.Generic.List<(Point start, Point end)> GenerateLineSegments(double cx, double cy,
                                     double halfW, double halfH,
                                     double step,
                                     double angleRadRect,
                                     double lineAngleDeg)
        {
            double angleLocal = lineAngleDeg * Math.PI / 180.0 - angleRadRect;
            double dirX = Math.Cos(angleLocal);
            double dirY = Math.Sin(angleLocal);
            double nx = -dirY;
            double ny = dirX;

            (double wx, double wy) ToWorld(double lx, double ly)
                => (cx + lx * Math.Cos(angleRadRect) - ly * Math.Sin(angleRadRect),
                    cy + lx * Math.Sin(angleRadRect) + ly * Math.Cos(angleRadRect));

            var corners = new[]
            {
                (-halfW, -halfH),
                ( halfW, -halfH),
                ( halfW,  halfH),
                (-halfW,  halfH)
            };
            double minProj = corners.Min(c => c.Item1 * nx + c.Item2 * ny);
            double maxProj = corners.Max(c => c.Item1 * nx + c.Item2 * ny);

            var offsets = new System.Collections.Generic.List<double>();
            for (double t = minProj; t <= maxProj + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxProj - 1e-6)
                offsets.Add(maxProj);

            var segments = new System.Collections.Generic.List<(Point start, Point end)>();

            foreach (var t in offsets)
            {
                var pts = new System.Collections.Generic.List<(double x, double y, double s)>();

                if (Math.Abs(dirX) > 1e-9)
                {
                    foreach (var sign in new[] { -1.0, 1.0 })
                    {
                        double s = (sign * halfW - nx * t) / dirX;
                        double y = ny * t + dirY * s;
                        if (y >= -halfH - 1e-6 && y <= halfH + 1e-6)
                            pts.Add((sign * halfW, y, s));
                    }
                }
                if (Math.Abs(dirY) > 1e-9)
                {
                    foreach (var sign in new[] { -1.0, 1.0 })
                    {
                        double s = (sign * halfH - ny * t) / dirY;
                        double x = nx * t + dirX * s;
                        if (x >= -halfW - 1e-6 && x <= halfW + 1e-6)
                            pts.Add((x, sign * halfH, s));
                    }
                }

                if (pts.Count < 2)
                    continue;

                var ordered = pts.OrderBy(p => p.s).ToList();
                var pStart = ordered.First();
                var pEnd = ordered.Last();

                var sWorld = ToWorld(pStart.x, pStart.y);
                var eWorld = ToWorld(pEnd.x, pEnd.y);

                segments.Add((new Point(sWorld.wx, sWorld.wy), new Point(eWorld.wx, eWorld.wy)));
            }

            return segments;
        }

        private void TravelAlongRectangle(Action<string> addLine, string g1,
                                          string fmt, CultureInfo culture,
                                          Point from, Point to,
                                          double cx, double cy,
                                          double halfW, double halfH,
                                          MillingDirection direction,
                                          double feedXYWork)
        {
            var corners = new[]
            {
                new Point(cx - halfW, cy - halfH), // 0 bottom-left
                new Point(cx + halfW, cy - halfH), // 1 bottom-right
                new Point(cx + halfW, cy + halfH), // 2 top-right
                new Point(cx - halfW, cy + halfH)  // 3 top-left
            };

            int Side(Point p)
            {
                double eps = 1e-4;
                if (Math.Abs(p.Y - (cy - halfH)) < eps) return 0; // bottom
                if (Math.Abs(p.X - (cx + halfW)) < eps) return 1; // right
                if (Math.Abs(p.Y - (cy + halfH)) < eps) return 2; // top
                return 3; // left
            }

            int startSide = Side(from);
            int endSide = Side(to);
            int step = direction == MillingDirection.Clockwise ? -1 : 1;

            // Добавляем промежуточные углы
            var path = new System.Collections.Generic.List<Point>();
            path.Add(from);

            int s = startSide;
            while (s != endSide)
            {
                s = (s + step + 4) % 4;
                path.Add(corners[s]);
            }

            path.Add(to);

            for (int i = 1; i < path.Count; i++)
            {
                var p = path[i];
                addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
        }

        private (double x, double y) GenerateLines(Action<string> addLine,
                                    string g0, string g1,
                                    string fmt, CultureInfo culture,
                                    double cx, double cy,
                                    double halfW, double halfH,
                                    double step,
                                    double angleRadRect,
                                    MillingDirection direction,
                                    double lineAngleDeg,
                                    double feedXYRapid,
                                    double feedXYWork,
                                    double cutZ,
                                    double safeZ,
                                    double feedZRapid)
        {
            double angleLocal = lineAngleDeg * Math.PI / 180.0 - angleRadRect;
            double dirX = Math.Cos(angleLocal);
            double dirY = Math.Sin(angleLocal);
            double nx = -dirY;
            double ny = dirX;

            // Вспомогательные функции
            (double wx, double wy) ToWorld(double lx, double ly)
                => (cx + lx * Math.Cos(angleRadRect) - ly * Math.Sin(angleRadRect),
                    cy + lx * Math.Sin(angleRadRect) + ly * Math.Cos(angleRadRect));

            // Минимальный/максимальный оффсет по нормали
            var corners = new[]
            {
                (-halfW, -halfH),
                ( halfW, -halfH),
                ( halfW,  halfH),
                (-halfW,  halfH)
            };
            double minProj = corners.Min(c => c.Item1 * nx + c.Item2 * ny);
            double maxProj = corners.Max(c => c.Item1 * nx + c.Item2 * ny);

            var offsets = new System.Collections.Generic.List<double>();
            for (double t = minProj; t <= maxProj + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxProj - 1e-6)
                offsets.Add(maxProj);

            (double x, double y) lastHit = ToWorld(halfW, -halfH); // fallback
            bool first = true;

            foreach (var t in offsets)
            {
                // Пересечения прямой p = n*t + dir*s с прямоугольником в локальных координатах
                var pts = new System.Collections.Generic.List<(double x, double y, double s)>();

                // x = ±halfW
                if (Math.Abs(dirX) > 1e-9)
                {
                    foreach (var sign in new[] { -1.0, 1.0 })
                    {
                        double s = (sign * halfW - nx * t) / dirX;
                        double y = ny * t + dirY * s;
                        if (y >= -halfH - 1e-6 && y <= halfH + 1e-6)
                            pts.Add((sign * halfW, y, s));
                    }
                }
                // y = ±halfH
                if (Math.Abs(dirY) > 1e-9)
                {
                    foreach (var sign in new[] { -1.0, 1.0 })
                    {
                        double s = (sign * halfH - ny * t) / dirY;
                        double x = nx * t + dirX * s;
                        if (x >= -halfW - 1e-6 && x <= halfW + 1e-6)
                            pts.Add((x, sign * halfH, s));
                    }
                }

                if (pts.Count < 2)
                    continue;

                // выбрать две крайние точки по s (минимум и максимум)
                var ordered = pts.OrderBy(p => p.s).ToList();
                var pStart = ordered.First();
                var pEnd = ordered.Last();

                var sWorld = ToWorld(pStart.x, pStart.y);
                var eWorld = ToWorld(pEnd.x, pEnd.y);

                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{sWorld.wx.ToString(fmt, culture)} Y{sWorld.wy.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{cutZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} X{eWorld.wx.ToString(fmt, culture)} Y{eWorld.wy.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                lastHit = (eWorld.wx, eWorld.wy);
            }

            return lastHit;
        }

        /// <summary>
        /// Классический алгоритм «концентрические прямоугольники».
        /// </summary>
        private void GenerateConcentricRectangles(Action<string> addLine,
                                                  string g1,
                                                  string fmt, CultureInfo culture,
                                                  double cx, double cy,
                                                  double halfW, double halfH,
                                                  MillingDirection direction,
                                                  double step,
                                                  double angleRad,
                                                  double feedXYWork,
                                                  bool onlyOuter = false,
                                                  Point? startPoint = null)
        {
            var minHalf = Math.Min(halfW, halfH);
            var offsets = new System.Collections.Generic.List<double>();
            var maxOffset = minHalf - 1e-6;
            for (double o = 0; o <= maxOffset; o += step)
                offsets.Add(o);
            if (offsets.Count == 0 || offsets.Last() < maxOffset)
                offsets.Add(maxOffset);
            offsets = offsets.OrderBy(v => v).ToList(); // 0 – внешний, maxOffset – внутренний

            if (onlyOuter && offsets.Count > 0)
            {
                // для чистовой по стенкам и внешнего обхода используем только внешний контур
                offsets = new System.Collections.Generic.List<double> { 0.0 };
            }
            else
            {
                // для стратегии «концентрические линии» обрабатываем из центра к краю:
                // сначала внутренний прямоугольник, затем наружные
                offsets.Reverse();
            }

            var clockwise = direction == MillingDirection.Clockwise;

            // Функция поворота координат
            (double X, double Y) Rot(double x, double y)
                => (cx + x * Math.Cos(angleRad) - y * Math.Sin(angleRad),
                    cy + x * Math.Sin(angleRad) + y * Math.Cos(angleRad));

            foreach (var offset in offsets)
            {
                var w = halfW - offset;
                var h = halfH - offset;
                if (w <= 0 || h <= 0) break;

                var corners = new[]
                {
                    Rot(-w, -h), // bottom-left
                    Rot( w, -h), // bottom-right
                    Rot( w,  h), // top-right
                    Rot(-w,  h), // top-left
                };

                int[] order = clockwise ? new[] { 0, 3, 2, 1, 0 } : new[] { 0, 1, 2, 3, 0 };

                var poly = order.Select(idx => new Point(corners[idx].X, corners[idx].Y)).ToList();

                if (startPoint.HasValue)
                {
                    var sp = startPoint.Value;
                    // Найти ближайший сегмент и вставить startPoint
                    int bestEdge = 0;
                    double bestDist = double.MaxValue;
                    for (int i = 0; i < poly.Count - 1; i++)
                    {
                        var a = poly[i];
                        var b = poly[i + 1];
                        double dist = DistancePointToSegment(sp.X, sp.Y, a.X, a.Y, b.X, b.Y);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestEdge = i;
                        }
                    }

                    // Вставим startPoint в найденное место
                    poly.Insert(bestEdge + 1, new Point(sp.X, sp.Y));

                    // Повернём список, чтобы startPoint был первым
                    int startIdx = bestEdge + 1;
                    poly = poly.Skip(startIdx).Concat(poly.Take(startIdx)).ToList();
                    // Замкнём контур
                    if (!(poly.Count > 0 && poly[0].X == poly[poly.Count - 1].X && poly[0].Y == poly[poly.Count - 1].Y))
                        poly.Add(poly[0]);
                }

                // Соединяем с последней точкой
                addLine($"{g1} X{poly[0].X.ToString(fmt, culture)} Y{poly[0].Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                for (int i = 1; i < poly.Count; i++)
                {
                    var p = poly[i];
                    addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
            }
        }

        private double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
                return Math.Sqrt(Math.Pow(px - x1, 2) + Math.Pow(py - y1, 2));

            double t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));
            double projX = x1 + t * dx;
            double projY = y1 + t * dy;
            return Math.Sqrt(Math.Pow(px - projX, 2) + Math.Pow(py - projY, 2));
        }

        /// <summary>
        /// Вычисление центра прямоугольника в зависимости от точки отсчёта.
        /// </summary>
        private void GetCenter(ReferencePointType type,
                               double refX, double refY,
                               double width, double height,
                               out double cx, out double cy)
        {
            switch (type)
            {
                case ReferencePointType.Center:
                    cx = refX; cy = refY; break;
                case ReferencePointType.TopLeft:
                    cx = refX + width / 2.0; cy = refY - height / 2.0; break;
                case ReferencePointType.TopRight:
                    cx = refX - width / 2.0; cy = refY - height / 2.0; break;
                case ReferencePointType.BottomLeft:
                    cx = refX + width / 2.0; cy = refY + height / 2.0; break;
                case ReferencePointType.BottomRight:
                    cx = refX - width / 2.0; cy = refY + height / 2.0; break;
                default:
                    cx = refX; cy = refY; break;
            }
        }

        #endregion
    }
}
