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

            // Проверяем, включено ли фрезерование острова
            if (op.IsIslandMillingEnabled)
            {
                GenerateIslandMilling(op, addLine, g0, g1, settings);
                return;
            }

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
                                   op.FeedXYRapid,
                                   op.SafeZHeight, nextZ,
                                   op.FeedZRapid, op.FeedZWork,
                                   op.RetractHeight);
                else if (op.PocketStrategy == PocketStrategy.Radial)
                {
                    var lastHit = GenerateRadial(addLine, g0, g1,
                                   fmt, culture,
                                   cx, cy, halfW, halfH,
                                   step, angleRad,
                                   op.Direction,
                                   op.FeedXYWork,
                                   op.FeedXYRapid,
                                   op.FeedZRapid,
                                   nextZ,
                                   op.RetractHeight);
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
                                TravelAlongRectangle(addLine, g0, g1, fmt, culture, end, nextStart, cx, cy, halfW, halfH, op.Direction, op.FeedXYWork, op.FeedXYRapid, op.FeedZRapid, nextZ, op.RetractHeight);
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
                // сначала уходим в центр кармана на холостом ходу с подъемом, затем поднимаемся.
                double retractZ = nextZ + op.RetractHeight;
                addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

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
                                    double feedXYRapid,
                                    double safeZ, double currentZ,
                                    double feedZRapid, double feedZWork,
                                    double retractHeight)
        {
            // Работаем в локальной системе кармана (центр в 0,0, оси вдоль сторон),
            // затем поворачиваем на angleRad и смещаем в (cx, cy).
            double cosRot = Math.Cos(angleRad);
            double sinRot = Math.Sin(angleRad);

            (double lx, double ly) ToLocal(double x, double y)
                => ((x - cx) * cosRot + (y - cy) * sinRot,
                    -(x - cx) * sinRot + (y - cy) * cosRot);

            (double x, double y) ToWorld(double lx, double ly)
                => (cx + lx * cosRot - ly * sinRot,
                    cy + lx * sinRot + ly * cosRot);

            // Максимальный радиус спирали – до угла прямоугольника в локальных координатах
            var maxRadius = Math.Sqrt(halfW * halfW + halfH * halfH);

            const double a = 0.0;                        // r(θ) = a + b·θ
            double b = step / (2 * Math.PI);             // радиальная скорость за один оборот

            int pointsPerRevolution = 128;
            double angleStep = 2 * Math.PI / pointsPerRevolution;
            double dirSign = direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            double θMax = (maxRadius - a) / b;

            // Границы прямоугольника в ЛОКАЛЬНОЙ системе
            double left = -halfW;
            double right = halfW;
            double bottom = -halfH;
            double top = halfH;

            // Функция проверки, находится ли точка внутри прямоугольника (строго внутри, не на границе)
            bool IsInside(double x, double y)
            {
                var p = ToLocal(x, y);
                double tolerance = 1e-6;
                return p.lx > left + tolerance && p.lx < right - tolerance &&
                       p.ly > bottom + tolerance && p.ly < top - tolerance;
            }

            // Функция проверки, находится ли точка на границе или внутри
            bool IsOnOrInside(double x, double y)
            {
                var p = ToLocal(x, y);
                return p.lx >= left && p.lx <= right && p.ly >= bottom && p.ly <= top;
            }

            // Функция нахождения точки пересечения отрезка с границей прямоугольника
            // Входные координаты – МИРОВЫЕ, внутри считаем в ЛОКАЛЬНЫХ и возвращаем в мир.
            bool FindIntersection(double x1, double y1, double x2, double y2,
                                 out double ix, out double iy)
            {
                var p1 = ToLocal(x1, y1);
                var p2 = ToLocal(x2, y2);

                double lx1 = p1.lx, ly1 = p1.ly;
                double lx2 = p2.lx, ly2 = p2.ly;

                double dx = lx2 - lx1;
                double dy = ly2 - ly1;
                ix = x2;
                iy = y2;

                if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9) return false;

                double tMin = 0.0;
                double tMax = 1.0;
                bool found = false;

                if (Math.Abs(dx) > 1e-9)
                {
                    double tLeft = (left - lx1) / dx;
                    double tRight = (right - lx1) / dx;
                    if (tLeft > tRight) { double tmp = tLeft; tLeft = tRight; tRight = tmp; }
                    if (tLeft >= 0 && tLeft <= 1 && tLeft > tMin) { tMin = tLeft; found = true; }
                    if (tRight >= 0 && tRight <= 1 && tRight < tMax) { tMax = tRight; found = true; }
                }

                if (Math.Abs(dy) > 1e-9)
                {
                    double tBottom = (bottom - ly1) / dy;
                    double tTop = (top - ly1) / dy;
                    if (tBottom > tTop) { double tmp = tBottom; tBottom = tTop; tTop = tmp; }
                    if (tBottom >= 0 && tBottom <= 1 && tBottom > tMin) { tMin = tBottom; found = true; }
                    if (tTop >= 0 && tTop <= 1 && tTop < tMax) { tMax = tTop; found = true; }
                }

                if (!found || tMin > tMax || tMin < 0 || tMin > 1) return false;

                double ilx = lx1 + tMin * dx;
                double ily = ly1 + tMin * dy;
                var w = ToWorld(ilx, ily);
                ix = w.x;
                iy = w.y;
                return true;
            }

            // Функция движения по контуру от точки (x1, y1) до точки (x2, y2) в МИРОВОЙ системе
            // Проходит через все углы между этими точками (по кратчайшему пути)
            // Возвращает true, если путь построен успешно; false - если нужен подъём инструмента
            bool MoveAlongContour(double x1, double y1, double x2, double y2, double zLevel, double retractH, double feedRapid)
            {
                double tolerance = 1e-4;

                // Углы прямоугольника в ЛОКАЛЬНОЙ системе (против часовой стрелки)
                var cornersLocal = new[]
                {
                    (left,  bottom),   // 0: левый нижний
                    (right, bottom),   // 1: правый нижний
                    (right, top),      // 2: правый верхний
                    (left,  top),      // 3: левый верхний
                };

                var lp1 = ToLocal(x1, y1);
                var lp2 = ToLocal(x2, y2);

                int GetSide(double px, double py)
                {
                    if (Math.Abs(py - bottom) < tolerance) return 0; // нижняя
                    if (Math.Abs(px - right) < tolerance) return 1; // правая
                    if (Math.Abs(py - top) < tolerance) return 2; // верхняя
                    if (Math.Abs(px - left) < tolerance) return 3; // левая
                    return -1;
                }

                int sideStart = GetSide(lp1.lx, lp1.ly);
                int sideEnd = GetSide(lp2.lx, lp2.ly);

                if (sideStart < 0 || sideEnd < 0) return false;

                // Вычисляем высоту отвода один раз
                double retractZ = zLevel + retractH;

                if (sideStart == sideEnd)
                {
                    // На одной стороне – поднимаем, переходим, опускаем
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{zLevel.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    return true;
                }

                // Вычисляем расстояние в обоих направлениях по контуру (в локальных координатах)
                double DistCCW()
                {
                    double dist = 0;
                    double px = lp1.lx, py = lp1.ly;
                    int side = sideStart;
                    while (side != sideEnd)
                    {
                        int cornerIdx = (side + 1) % 4;
                        dist += Math.Sqrt(Math.Pow(px - cornersLocal[cornerIdx].Item1, 2) + Math.Pow(py - cornersLocal[cornerIdx].Item2, 2));
                        px = cornersLocal[cornerIdx].Item1;
                        py = cornersLocal[cornerIdx].Item2;
                        side = (side + 1) % 4;
                    }
                    dist += Math.Sqrt(Math.Pow(px - lp2.lx, 2) + Math.Pow(py - lp2.ly, 2));
                    return dist;
                }

                double DistCW()
                {
                    double dist = 0;
                    double px = lp1.lx, py = lp1.ly;
                    int side = sideStart;
                    while (side != sideEnd)
                    {
                        int cornerIdx = side;
                        dist += Math.Sqrt(Math.Pow(px - cornersLocal[cornerIdx].Item1, 2) + Math.Pow(py - cornersLocal[cornerIdx].Item2, 2));
                        px = cornersLocal[cornerIdx].Item1;
                        py = cornersLocal[cornerIdx].Item2;
                        side = (side + 3) % 4;
                    }
                    dist += Math.Sqrt(Math.Pow(px - lp2.lx, 2) + Math.Pow(py - lp2.ly, 2));
                    return dist;
                }

                bool ccw = DistCCW() <= DistCW();

                // Поднимаем инструмент для перехода
                addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");

                int currentSide = sideStart;
                while (currentSide != sideEnd)
                {
                    int cornerIdx = ccw ? (currentSide + 1) % 4 : currentSide;
                    var wCorner = ToWorld(cornersLocal[cornerIdx].Item1, cornersLocal[cornerIdx].Item2);
                    addLine($"{g0} X{wCorner.x.ToString(fmt, culture)} Y{wCorner.y.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                    currentSide = ccw ? (currentSide + 1) % 4 : (currentSide + 3) % 4;
                }

                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");

                // Опускаем обратно на рабочую высоту
                addLine($"{g0} Z{zLevel.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                return true;
            }
            
            // Функция для перемещения с подъёмом инструмента
            void MoveWithRetract(double x1, double y1, double x2, double y2)
            {
                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            }

            // Сначала собираем все точки спирали (в МИРОВЫХ координатах, но строим её в локальных)
            var spiralPoints = new System.Collections.Generic.List<(double x, double y)>();
            spiralPoints.Add((cx, cy));

            for (double θ = angleStep; θ <= θMax + 1e-9; θ += angleStep)
            {
                double r = a + b * θ;
                double ang = θ * dirSign;

                double lx = r * Math.Cos(ang);
                double ly = r * Math.Sin(ang);
                var w = ToWorld(lx, ly);
                spiralPoints.Add((w.x, w.y));
            }

            // Функция clamp - ограничивает точку контуром (локально) и возвращает в мир
            (double x, double y) Clamp(double x, double y)
            {
                double lx = Math.Max(left, Math.Min(right, x));
                double ly = Math.Max(bottom, Math.Min(top, y));
                var w = ToWorld(lx, ly);
                return (w.x, w.y);
            }

            // Функция для нахождения точки пересечения отрезка с границей прямоугольника
            // Находит ВСЕ пересечения в ЛОКАЛЬНЫХ координатах и возвращает ближайшее к (x1, y1)
            (double x, double y) FindBorderIntersection(double x1, double y1, double x2, double y2)
            {
                var lp1 = ToLocal(x1, y1);
                var lp2 = ToLocal(x2, y2);

                double dx = lp2.lx - lp1.lx;
                double dy = lp2.ly - lp1.ly;

                var intersections = new System.Collections.Generic.List<(double t, double x, double y)>();

                // Пересечение с левой стороной (x = left)
                if (Math.Abs(dx) > 1e-9)
                {
                    double t = (left - lp1.lx) / dx;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double yInt = lp1.ly + t * dy;
                        if (yInt >= bottom - 1e-9 && yInt <= top + 1e-9)
                            intersections.Add((t, left, Math.Max(bottom, Math.Min(top, yInt))));
                    }
                }

                // Пересечение с правой стороной (x = right)
                if (Math.Abs(dx) > 1e-9)
                {
                    double t = (right - lp1.lx) / dx;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double yInt = lp1.ly + t * dy;
                        if (yInt >= bottom - 1e-9 && yInt <= top + 1e-9)
                            intersections.Add((t, right, Math.Max(bottom, Math.Min(top, yInt))));
                    }
                }

                // Пересечение с нижней стороной (y = bottom)
                if (Math.Abs(dy) > 1e-9)
                {
                    double t = (bottom - lp1.ly) / dy;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double xInt = lp1.lx + t * dx;
                        if (xInt >= left - 1e-9 && xInt <= right + 1e-9)
                            intersections.Add((t, Math.Max(left, Math.Min(right, xInt)), bottom));
                    }
                }

                // Пересечение с верхней стороной (y = top)
                if (Math.Abs(dy) > 1e-9)
                {
                    double t = (top - lp1.ly) / dy;
                    if (t > 1e-9 && t < 1 - 1e-9)
                    {
                        double xInt = lp1.lx + t * dx;
                        if (xInt >= left - 1e-9 && xInt <= right + 1e-9)
                            intersections.Add((t, Math.Max(left, Math.Min(right, xInt)), top));
                    }
                }

                if (intersections.Count == 0)
                {
                    var clamped = Clamp(lp2.lx, lp2.ly);
                    return (clamped.x, clamped.y);
                }

                intersections.Sort((p1, p2) => p1.t.CompareTo(p2.t));
                var best = intersections[0];
                var wBest = ToWorld(best.x, best.y);
                return (wBest.x, wBest.y);
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
                        if (!MoveAlongContour(exitX, exitY, entry.x, entry.y, currentZ, retractHeight, feedXYRapid))
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

            // Переходим в локальные координаты для определения стороны и углов
            var startLocal = ToLocal(startX, startY);
            double slx = startLocal.lx;
            double sly = startLocal.ly;

            double eps = 1e-4;
            int GetSideSimpleLocal(double x, double y)
            {
                if (Math.Abs(y - bottom) < eps) return 0; // нижняя
                if (Math.Abs(x - right) < eps) return 1;  // правая
                if (Math.Abs(y - top) < eps) return 2;    // верхняя
                if (Math.Abs(x - left) < eps) return 3;   // левая
                return -1;
            }

            // Углы прямоугольника в ЛОКАЛЬНЫХ координатах (против часовой стрелки, начиная с левого нижнего)
            var rectCornersLocal = new[]
            {
                (left,  bottom), // 0
                (right, bottom), // 1
                (right, top),    // 2
                (left,  top),    // 3
            };

            int startSide = GetSideSimpleLocal(slx, sly);

            // Полный обход контура от текущей точки против часовой стрелки (в мире)
            if (startSide >= 0)
            {
                // Сначала идём к ближайшему углу на текущей стороне (против часовой стрелки)
                int firstCorner = (startSide + 1) % 4;
                var firstWorld = ToWorld(rectCornersLocal[firstCorner].Item1, rectCornersLocal[firstCorner].Item2);
                addLine($"{g1} X{firstWorld.x.ToString(fmt, culture)} Y{firstWorld.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                // Проходим оставшиеся 3 угла
                for (int i = 1; i <= 3; i++)
                {
                    int cornerIdx = (firstCorner + i) % 4;
                    var cw = ToWorld(rectCornersLocal[cornerIdx].Item1, rectCornersLocal[cornerIdx].Item2);
                    addLine($"{g1} X{cw.x.ToString(fmt, culture)} Y{cw.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }

                // Возвращаемся к начальной точке
                addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
            else
            {
                // Если не на контуре — поднимаем инструмент и делаем полный обход по локальным углам
                var bottomLeftWorld = ToWorld(left, bottom);
                var bottomRightWorld = ToWorld(right, bottom);
                var topRightWorld = ToWorld(right, top);
                var topLeftWorld = ToWorld(left, top);

                MoveWithRetract(startX, startY, bottomLeftWorld.x, bottomLeftWorld.y);
                addLine($"{g1} X{bottomRightWorld.x.ToString(fmt, culture)} Y{bottomRightWorld.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{topRightWorld.x.ToString(fmt, culture)} Y{topRightWorld.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{topLeftWorld.x.ToString(fmt, culture)} Y{topLeftWorld.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{bottomLeftWorld.x.ToString(fmt, culture)} Y{bottomLeftWorld.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
        }


        /// <summary>
        /// Радиальные линии для прямоугольного кармана.
        /// Центр → граница, шаг по контуру, возврат в центр.
        /// </summary>
        private (double x, double y) GenerateRadial(Action<string> addLine,
                                    string g0,
                                    string g1,
                                    string fmt, CultureInfo culture,
                                    double cx, double cy,
                                    double halfW, double halfH,
                                    double step,
                                    double angleRad,
                                    MillingDirection direction,
                                    double feedXYWork,
                                    double feedXYRapid,
                                    double feedZRapid,
                                    double currentZ,
                                    double retractHeight)
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
                
                // Переход в центр на холостом ходу с подъемом
                double retractZ = currentZ + retractHeight;
                addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{cx.ToString(fmt, culture)} Y{cy.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");

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

        private void TravelAlongRectangle(Action<string> addLine, string g0, string g1,
                                          string fmt, CultureInfo culture,
                                          Point from, Point to,
                                          double cx, double cy,
                                          double halfW, double halfH,
                                          MillingDirection direction,
                                          double feedXYWork,
                                          double feedXYRapid,
                                          double feedZRapid,
                                          double currentZ,
                                          double retractHeight)
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

            // Поднимаем инструмент для перехода
            double retractZ = currentZ + retractHeight;
            addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");

            // Перемещаемся по контуру на холостом ходу
            for (int i = 1; i < path.Count; i++)
            {
                var p = path[i];
                addLine($"{g0} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYRapid.ToString(fmt, culture)}");
            }

            // Опускаем обратно на рабочую высоту
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
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

        /// <summary>
        /// Генерация траектории для фрезерования острова (обработка области вокруг острова).
        /// </summary>
        private void GenerateIslandMilling(PocketRectangleOperation op,
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

            // ----------- параметры острова (текущий прямоугольный карман) ----------
            GetCenter(op.ReferencePointType, op.ReferencePointX, op.ReferencePointY,
                      op.Width, op.Height, out double islandCx, out double islandCy);

            double islandHalfW = op.Width / 2.0;
            double islandHalfH = op.Height / 2.0;
            
            // Контур острова с учетом радиуса фрезы (снаружи острова)
            double islandContourHalfW = islandHalfW + toolRadius;
            double islandContourHalfH = islandHalfH + toolRadius;

            var islandAngleRad = op.RotationAngle * Math.PI / 180.0;
            var islandCos = Math.Cos(islandAngleRad);
            var islandSin = Math.Sin(islandAngleRad);

            // ----------- параметры внешней границы ----------
            double outerCx = op.OuterBoundaryCenterX;
            double outerCy = op.OuterBoundaryCenterY;
            double outerHalfW = op.OuterBoundaryWidth / 2.0;
            double outerHalfH = op.OuterBoundaryHeight / 2.0;

            // Внутренний контур внешней границы с учетом радиуса фрезы
            double outerContourHalfW = outerHalfW - toolRadius;
            double outerContourHalfH = outerHalfH - toolRadius;

            if (outerContourHalfW <= islandContourHalfW || outerContourHalfH <= islandContourHalfH)
            {
                if (settings.UseComments)
                    addLine("(Outer boundary too small for island milling, skipping)");
                return;
            }

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
                    addLine($"(Island milling pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                // Переходы в безопасную высоту и начало работы
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                
                // Определяем начальную точку (на внешней границе)
                double startX, startY;
                if (op.OuterBoundaryType == OuterBoundaryType.Rectangle)
                {
                    // Для прямоугольника - левый нижний угол
                    startX = outerCx - outerContourHalfW;
                    startY = outerCy - outerContourHalfH;
                }
                else // Ellipse
                {
                    // Для эллипса - точка на левой стороне
                    startX = outerCx - outerContourHalfW;
                    startY = outerCy;
                }

                addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                // Понижение и начало резки
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                // ----------- генерация траектории в зависимости от стратегии ----------
                if (op.PocketStrategy == PocketStrategy.Spiral)
                {
                    // Используем спираль как во внутренней обработке
                    GenerateIslandSpiral(addLine, g0, g1, fmt, culture,
                                        outerCx, outerCy, outerContourHalfW, outerContourHalfH,
                                        op.OuterBoundaryType,
                                        islandCx, islandCy, islandContourHalfW, islandContourHalfH,
                                        islandAngleRad, islandCos, islandSin,
                                        step, op.Direction,
                                        op.FeedXYWork, op.FeedXYRapid,
                                        nextZ, op.SafeZHeight,
                                        op.FeedZRapid, op.RetractHeight);
                }
                else
                {
                    // Для других стратегий используем концентрические контуры
                    if (op.OuterBoundaryType == OuterBoundaryType.Rectangle)
                    {
                        GenerateIslandMillingRectangles(addLine, g1, fmt, culture,
                                                        outerCx, outerCy, outerContourHalfW, outerContourHalfH,
                                                        islandCx, islandCy, islandContourHalfW, islandContourHalfH,
                                                        islandAngleRad, islandCos, islandSin,
                                                        step, op.Direction, op.FeedXYWork);
                    }
                    else // Ellipse
                    {
                        // Для эллипса используем концентрические эллипсы
                        GenerateIslandMillingEllipses(addLine, g1, fmt, culture,
                                                      outerCx, outerCy, outerContourHalfW, outerContourHalfH,
                                                      islandCx, islandCy, islandContourHalfW, islandContourHalfH,
                                                      islandAngleRad, islandCos, islandSin,
                                                      step, op.Direction, op.FeedXYWork);
                    }

                    // ----------- проход по контуру острова снаружи ----------
                    GenerateIslandContour(addLine, g1, fmt, culture,
                                          islandCx, islandCy, islandContourHalfW, islandContourHalfH,
                                          islandAngleRad, islandCos, islandSin,
                                          op.Direction, op.FeedXYWork);
                }

                // Подъем инструмента
                double retractZ = nextZ + op.RetractHeight;
                addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                currentZ = nextZ;
            }
        }

        /// <summary>
        /// Генерация концентрических контуров для области между внешней границей и островом (прямоугольники).
        /// </summary>
        private void GenerateIslandMillingRectangles(Action<string> addLine,
                                                      string g1,
                                                      string fmt,
                                                      CultureInfo culture,
                                                      double outerCx, double outerCy,
                                                      double outerHalfW, double outerHalfH,
                                                      double islandCx, double islandCy,
                                                      double islandHalfW, double islandHalfH,
                                                      double islandAngleRad,
                                                      double islandCos, double islandSin,
                                                      double step,
                                                      MillingDirection direction,
                                                      double feedXYWork)
        {
            // Вычисляем минимальное расстояние от внешней границы до острова
            // Упрощенный подход: используем минимальную полуось внешней границы и максимальную полуось острова
            double minOuter = Math.Min(outerHalfW, outerHalfH);
            double maxIsland = Math.Max(islandHalfW, islandHalfH);
            double maxOffset = minOuter - maxIsland - 1e-6;

            if (maxOffset <= 0)
                return;

            // Генерируем концентрические контуры от внешней границы к острову
            var offsets = new System.Collections.Generic.List<double>();
            for (double o = 0; o <= maxOffset; o += step)
                offsets.Add(o);
            if (offsets.Count == 0 || offsets.Last() < maxOffset)
                offsets.Add(maxOffset);

            // Обрабатываем от внешней границы к острову
            var clockwise = direction == MillingDirection.Clockwise;

            foreach (var offset in offsets)
            {
                var w = outerHalfW - offset;
                var h = outerHalfH - offset;

                // Проверяем, не пересекается ли контур с островом
                // Упрощенная проверка: если контур меньше острова, пропускаем
                if (w <= islandHalfW || h <= islandHalfH)
                    break;

                // Генерируем контур прямоугольника
                var corners = new[]
                {
                    (outerCx - w, outerCy - h), // bottom-left
                    (outerCx + w, outerCy - h), // bottom-right
                    (outerCx + w, outerCy + h), // top-right
                    (outerCx - w, outerCy + h), // top-left
                };

                int[] order = clockwise ? new[] { 0, 3, 2, 1, 0 } : new[] { 0, 1, 2, 3, 0 };

                // Генерируем траекторию по контуру с обрезкой по границе острова
                var pathPoints = new System.Collections.Generic.List<(double x, double y)>();
                
                for (int i = 0; i < order.Length - 1; i++)
                {
                    var p1 = corners[order[i]];
                    var p2 = corners[order[(i + 1) % (order.Length - 1)]];
                    
                    // Проверяем, пересекается ли сторона с островом
                    var clippedSegment = ClipSegmentByIsland(p1.Item1, p1.Item2, p2.Item1, p2.Item2,
                                                             islandCx, islandCy, islandHalfW, islandHalfH,
                                                             islandCos, islandSin);
                    
                    if (clippedSegment != null)
                    {
                        var (start, end) = clippedSegment.Value;
                        // Добавляем точки обрезанного отрезка
                        if (pathPoints.Count == 0 || 
                            Math.Abs(pathPoints[pathPoints.Count - 1].x - start.startX) > 1e-6 ||
                            Math.Abs(pathPoints[pathPoints.Count - 1].y - start.startY) > 1e-6)
                        {
                            pathPoints.Add((start.startX, start.startY));
                        }
                        pathPoints.Add((end.endX, end.endY));
                    }
                }
                
                // Генерируем G-код для обрезанного контура
                if (pathPoints.Count > 0)
                {
                    // Замыкаем контур, если нужно
                    if (pathPoints.Count > 1 && 
                        (Math.Abs(pathPoints[0].x - pathPoints[pathPoints.Count - 1].x) > 1e-6 ||
                         Math.Abs(pathPoints[0].y - pathPoints[pathPoints.Count - 1].y) > 1e-6))
                    {
                        pathPoints.Add(pathPoints[0]);
                    }
                    
                    foreach (var point in pathPoints)
                    {
                        addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    }
                }
            }
        }

        /// <summary>
        /// Обрезка отрезка по границе повернутого прямоугольника острова.
        /// Возвращает обрезанный отрезок (или null, если весь отрезок внутри острова).
        /// </summary>
        private ((double startX, double startY), (double endX, double endY))? ClipSegmentByIsland(
            double x1, double y1, double x2, double y2,
            double islandCx, double islandCy,
            double islandHalfW, double islandHalfH,
            double islandCos, double islandSin)
        {
            // Переводим точки отрезка в локальные координаты острова
            double dx1 = x1 - islandCx;
            double dy1 = y1 - islandCy;
            double dx2 = x2 - islandCx;
            double dy2 = y2 - islandCy;
            
            double localX1 = dx1 * islandCos - dy1 * islandSin;
            double localY1 = dx1 * islandSin + dy1 * islandCos;
            double localX2 = dx2 * islandCos - dy2 * islandSin;
            double localY2 = dx2 * islandSin + dy2 * islandCos;
            
            // Проверяем, находятся ли обе точки внутри острова
            bool p1Inside = Math.Abs(localX1) < islandHalfW && Math.Abs(localY1) < islandHalfH;
            bool p2Inside = Math.Abs(localX2) < islandHalfW && Math.Abs(localY2) < islandHalfH;
            
            if (p1Inside && p2Inside)
            {
                // Весь отрезок внутри острова - пропускаем
                return null;
            }
            
            if (!p1Inside && !p2Inside)
            {
                // Обе точки снаружи - проверяем, не пересекает ли отрезок остров
                // Находим точки пересечения с границами острова
                var intersections = new System.Collections.Generic.List<double>();
                
                double segDx = localX2 - localX1;
                double segDy = localY2 - localY1;
                
                if (Math.Abs(segDx) > 1e-9)
                {
                    double tLeft = (-islandHalfW - localX1) / segDx;
                    double tRight = (islandHalfW - localX1) / segDx;
                    if (tLeft >= 0 && tLeft <= 1)
                    {
                        double yAtLeft = localY1 + tLeft * segDy;
                        if (Math.Abs(yAtLeft) <= islandHalfH + 1e-6)
                            intersections.Add(tLeft);
                    }
                    if (tRight >= 0 && tRight <= 1)
                    {
                        double yAtRight = localY1 + tRight * segDy;
                        if (Math.Abs(yAtRight) <= islandHalfH + 1e-6)
                            intersections.Add(tRight);
                    }
                }
                
                if (Math.Abs(segDy) > 1e-9)
                {
                    double tBottom = (-islandHalfH - localY1) / segDy;
                    double tTop = (islandHalfH - localY1) / segDy;
                    if (tBottom >= 0 && tBottom <= 1)
                    {
                        double xAtBottom = localX1 + tBottom * segDx;
                        if (Math.Abs(xAtBottom) <= islandHalfW + 1e-6)
                            intersections.Add(tBottom);
                    }
                    if (tTop >= 0 && tTop <= 1)
                    {
                        double xAtTop = localX1 + tTop * segDx;
                        if (Math.Abs(xAtTop) <= islandHalfW + 1e-6)
                            intersections.Add(tTop);
                    }
                }
                
                intersections = intersections.Distinct().OrderBy(t => t).ToList();
                
                if (intersections.Count == 0)
                {
                    // Отрезок не пересекает остров - используем полностью
                    return ((x1, y1), (x2, y2));
                }
                
                if (intersections.Count == 2)
                {
                    // Отрезок входит и выходит из острова - возвращаем части снаружи
                    // Но для упрощения пока пропускаем такой отрезок
                    // TODO: вернуть две части - до входа и после выхода
                    return null;
                }
                
                // Одна точка пересечения - это не должно происходить при обеих точках снаружи
                // Используем весь отрезок
                return ((x1, y1), (x2, y2));
            }
            
            // Одна точка внутри, другая снаружи - находим точку пересечения
            double tIntersect = 0.0;
            bool found = false;
            
            // Находим параметр t, при котором отрезок пересекает границу острова
            double segDx2 = localX2 - localX1;
            double segDy2 = localY2 - localY1;
            
            if (Math.Abs(segDx2) > 1e-9)
            {
                double tLeft = (-islandHalfW - localX1) / segDx2;
                double tRight = (islandHalfW - localX1) / segDx2;
                if (tLeft >= 0 && tLeft <= 1 && (p1Inside ? tLeft > tIntersect : true)) { tIntersect = tLeft; found = true; }
                if (tRight >= 0 && tRight <= 1 && (p1Inside ? tRight > tIntersect : true)) { tIntersect = tRight; found = true; }
            }
            
            if (Math.Abs(segDy2) > 1e-9)
            {
                double tBottom = (-islandHalfH - localY1) / segDy2;
                double tTop = (islandHalfH - localY1) / segDy2;
                if (tBottom >= 0 && tBottom <= 1 && (p1Inside ? tBottom > tIntersect : true)) { tIntersect = tBottom; found = true; }
                if (tTop >= 0 && tTop <= 1 && (p1Inside ? tTop > tIntersect : true)) { tIntersect = tTop; found = true; }
            }
            
            if (!found)
            {
                // Не нашли пересечение - используем внешнюю точку
                if (p1Inside)
                    return ((x2, y2), (x2, y2));
                else
                    return ((x1, y1), (x1, y1));
            }
            
            // Вычисляем точку пересечения в локальных координатах
            double localX = localX1 + tIntersect * segDx2;
            double localY = localY1 + tIntersect * segDy2;
            
            // Переводим обратно в мировые координаты
            double worldX = islandCx + localX * islandCos - localY * islandSin;
            double worldY = islandCy + localX * islandSin + localY * islandCos;
            
            if (p1Inside)
            {
                // p1 внутри, p2 снаружи - используем от точки пересечения до p2
                return ((worldX, worldY), (x2, y2));
            }
            else
            {
                // p1 снаружи, p2 внутри - используем от p1 до точки пересечения
                return ((x1, y1), (worldX, worldY));
            }
        }
        
        /// <summary>
        /// Обрезка линии по прямоугольнику (алгоритм Лианга-Барски).
        /// </summary>
        private ((double startX, double startY), (double endX, double endY))? ClipLineByRectangle(
            double x1, double y1, double x2, double y2,
            double left, double bottom, double right, double top)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            
            double p1 = -dx, q1 = x1 - left;
            double p2 = dx, q2 = right - x1;
            double p3 = -dy, q3 = y1 - bottom;
            double p4 = dy, q4 = top - y1;
            
            double u1 = 0.0, u2 = 1.0;
            
            for (int i = 0; i < 4; i++)
            {
                double p = 0, q = 0;
                switch (i)
                {
                    case 0: p = p1; q = q1; break;
                    case 1: p = p2; q = q2; break;
                    case 2: p = p3; q = q3; break;
                    case 3: p = p4; q = q4; break;
                }
                
                if (Math.Abs(p) < 1e-9)
                {
                    if (q < 0) return null; // Линия параллельна и вне прямоугольника
                }
                else
                {
                    double r = q / p;
                    if (p < 0)
                    {
                        if (r > u1) u1 = r;
                    }
                    else
                    {
                        if (r < u2) u2 = r;
                    }
                }
            }
            
            if (u1 > u2) return null; // Линия полностью вне прямоугольника
            
            double clippedX1 = x1 + u1 * dx;
            double clippedY1 = y1 + u1 * dy;
            double clippedX2 = x1 + u2 * dx;
            double clippedY2 = y1 + u2 * dy;
            
            return ((clippedX1, clippedY1), (clippedX2, clippedY2));
        }

        /// <summary>
        /// Генерация траектории прохода по контуру острова снаружи.
        /// </summary>
        private void GenerateIslandContour(Action<string> addLine,
                                           string g1,
                                           string fmt,
                                           CultureInfo culture,
                                           double islandCx, double islandCy,
                                           double islandHalfW, double islandHalfH,
                                           double islandAngleRad,
                                           double islandCos, double islandSin,
                                           MillingDirection direction,
                                           double feedXYWork)
        {
            var clockwise = direction == MillingDirection.Clockwise;

            // Углы прямоугольника острова в локальных координатах
            var cornersLocal = new[]
            {
                (-islandHalfW, -islandHalfH), // bottom-left
                ( islandHalfW, -islandHalfH), // bottom-right
                ( islandHalfW,  islandHalfH), // top-right
                (-islandHalfW,  islandHalfH), // top-left
            };

            // Функция поворота координат
            (double X, double Y) Rot(double x, double y)
                => (islandCx + x * islandCos - y * islandSin,
                    islandCy + x * islandSin + y * islandCos);

            // Переводим углы в мировые координаты
            var corners = cornersLocal.Select(c => Rot(c.Item1, c.Item2)).ToArray();

            int[] order = clockwise ? new[] { 0, 3, 2, 1, 0 } : new[] { 0, 1, 2, 3, 0 };

            // Генерируем траекторию по контуру острова
            for (int i = 0; i < order.Length; i++)
            {
                var corner = corners[order[i]];
                addLine($"{g1} X{corner.X.ToString(fmt, culture)} Y{corner.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            }
        }

        /// <summary>
        /// Генерация спирали для фрезерования острова (аналогично GenerateSpiral, но с внешней границей и исключением острова).
        /// </summary>
        private void GenerateIslandSpiral(Action<string> addLine,
                                          string g0, string g1,
                                          string fmt, CultureInfo culture,
                                          double outerCx, double outerCy,
                                          double outerHalfW, double outerHalfH,
                                          OuterBoundaryType outerType,
                                          double islandCx, double islandCy,
                                          double islandHalfW, double islandHalfH,
                                          double islandAngleRad,
                                          double islandCos, double islandSin,
                                          double step,
                                          MillingDirection direction,
                                          double feedXYWork, double feedXYRapid,
                                          double currentZ, double safeZ,
                                          double feedZRapid, double retractHeight)
        {
            // Максимальный радиус спирали - до угла/края внешней границы
            double maxRadius;
            if (outerType == OuterBoundaryType.Rectangle)
            {
                maxRadius = Math.Sqrt(outerHalfW * outerHalfW + outerHalfH * outerHalfH);
            }
            else // Ellipse
            {
                maxRadius = Math.Max(outerHalfW, outerHalfH);
            }

            const double a = 0.0;
            double b = step / (2 * Math.PI);

            int pointsPerRevolution = 128;
            double angleStep = 2 * Math.PI / pointsPerRevolution;
            double dirSign = direction == MillingDirection.Clockwise ? -1.0 : 1.0;

            double θMax = (maxRadius - a) / b;

            // Функция проверки, находится ли точка внутри внешней границы и снаружи острова
            bool IsInsideValidArea(double x, double y)
            {
                // Проверяем, что точка внутри внешней границы
                bool insideOuter;
                if (outerType == OuterBoundaryType.Rectangle)
                {
                    double dx = x - outerCx;
                    double dy = y - outerCy;
                    double tolerance = 1e-6;
                    insideOuter = Math.Abs(dx) < outerHalfW - tolerance && Math.Abs(dy) < outerHalfH - tolerance;
                }
                else // Ellipse
                {
                    double dx = x - outerCx;
                    double dy = y - outerCy;
                    double normalizedX = dx / outerHalfW;
                    double normalizedY = dy / outerHalfH;
                    double tolerance = 1e-6;
                    insideOuter = (normalizedX * normalizedX + normalizedY * normalizedY) < 1.0 - tolerance;
                }

                if (!insideOuter) return false;

                // Проверяем, что точка снаружи острова
                // Переводим в локальные координаты острова (обратное преобразование)
                // Для поворота на угол angle обратное преобразование: поворот на -angle
                // cos(-angle) = cos(angle), sin(-angle) = -sin(angle)
                double dxIsland = x - islandCx;
                double dyIsland = y - islandCy;
                // Правильное обратное преобразование: поворот на -islandAngleRad
                double localX = dxIsland * islandCos + dyIsland * islandSin;
                double localY = -dxIsland * islandSin + dyIsland * islandCos;

                // Точка находится ВНУТРИ острова, если:
                // Math.Abs(localX) < islandHalfW && Math.Abs(localY) < islandHalfH
                // Точка находится СНАРУЖИ, если:
                // Math.Abs(localX) >= islandHalfW || Math.Abs(localY) >= islandHalfH
                double toleranceIsland = 1e-6;
                bool insideIsland = Math.Abs(localX) < islandHalfW - toleranceIsland && 
                                    Math.Abs(localY) < islandHalfH - toleranceIsland;
                
                return !insideIsland; // Возвращаем true, если точка снаружи
            }

            // Функция проверки, находится ли точка на границе или внутри допустимой области
            bool IsOnOrInsideValidArea(double x, double y)
            {
                // Проверяем внешнюю границу
                bool onOrInsideOuter;
                if (outerType == OuterBoundaryType.Rectangle)
                {
                    double dx = x - outerCx;
                    double dy = y - outerCy;
                    onOrInsideOuter = Math.Abs(dx) <= outerHalfW && Math.Abs(dy) <= outerHalfH;
                }
                else // Ellipse
                {
                    double dx = x - outerCx;
                    double dy = y - outerCy;
                    double normalizedX = dx / outerHalfW;
                    double normalizedY = dy / outerHalfH;
                    onOrInsideOuter = (normalizedX * normalizedX + normalizedY * normalizedY) <= 1.0;
                }

                if (!onOrInsideOuter) return false;

                // Проверяем, что точка снаружи острова (на границе или снаружи)
                // Переводим в локальные координаты острова (обратное преобразование)
                // Для поворота на угол angle обратное преобразование: поворот на -angle
                // cos(-angle) = cos(angle), sin(-angle) = -sin(angle)
                double dxIsland = x - islandCx;
                double dyIsland = y - islandCy;
                // Правильное обратное преобразование: поворот на -islandAngleRad
                double localX = dxIsland * islandCos + dyIsland * islandSin;
                double localY = -dxIsland * islandSin + dyIsland * islandCos;

                // Точка находится ВНУТРИ острова, если:
                // Math.Abs(localX) < islandHalfW && Math.Abs(localY) < islandHalfH
                // Точка находится СНАРУЖИ (включая границу), если:
                // Math.Abs(localX) >= islandHalfW || Math.Abs(localY) >= islandHalfH
                bool insideIsland = Math.Abs(localX) < islandHalfW && Math.Abs(localY) < islandHalfH;

                return !insideIsland; // Возвращаем true, если точка снаружи или на границе
            }

            // Функция нахождения точки пересечения с границей допустимой области
            (double x, double y) FindBorderIntersection(double x1, double y1, double x2, double y2)
            {
                // Ищем пересечение с внешней границей или границей острова
                // Сначала проверяем пересечение с внешней границей
                if (outerType == OuterBoundaryType.Rectangle)
                {
                    // Пересечение с прямоугольной внешней границей
                    var intersections = new System.Collections.Generic.List<(double t, double x, double y)>();

                    double dx = x2 - x1;
                    double dy = y2 - y1;

                    if (Math.Abs(dx) > 1e-9)
                    {
                        double tLeft = (outerCx - outerHalfW - x1) / dx;
                        if (tLeft > 1e-9 && tLeft < 1 - 1e-9)
                        {
                            double yInt = y1 + tLeft * dy;
                            if (Math.Abs(yInt - outerCy) <= outerHalfH + 1e-6)
                                intersections.Add((tLeft, outerCx - outerHalfW, yInt));
                        }

                        double tRight = (outerCx + outerHalfW - x1) / dx;
                        if (tRight > 1e-9 && tRight < 1 - 1e-9)
                        {
                            double yInt = y1 + tRight * dy;
                            if (Math.Abs(yInt - outerCy) <= outerHalfH + 1e-6)
                                intersections.Add((tRight, outerCx + outerHalfW, yInt));
                        }
                    }

                    if (Math.Abs(dy) > 1e-9)
                    {
                        double tBottom = (outerCy - outerHalfH - y1) / dy;
                        if (tBottom > 1e-9 && tBottom < 1 - 1e-9)
                        {
                            double xInt = x1 + tBottom * dx;
                            if (Math.Abs(xInt - outerCx) <= outerHalfW + 1e-6)
                                intersections.Add((tBottom, xInt, outerCy - outerHalfH));
                        }

                        double tTop = (outerCy + outerHalfH - y1) / dy;
                        if (tTop > 1e-9 && tTop < 1 - 1e-9)
                        {
                            double xInt = x1 + tTop * dx;
                            if (Math.Abs(xInt - outerCx) <= outerHalfW + 1e-6)
                                intersections.Add((tTop, xInt, outerCy + outerHalfH));
                        }
                    }

                    if (intersections.Count > 0)
                    {
                        intersections.Sort((p1, p2) => p1.t.CompareTo(p2.t));
                        return (intersections[0].x, intersections[0].y);
                    }
                }
                else // Ellipse
                {
                    // Пересечение с эллиптической внешней границей
                    // Упрощенный подход: используем ближайшую точку на эллипсе
                    double dx = x2 - x1;
                    double dy = y2 - y1;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 1e-9)
                    {
                        dx /= len;
                        dy /= len;
                        
                        // Ищем точку пересечения луча с эллипсом
                        // (x - cx)^2 / a^2 + (y - cy)^2 / b^2 = 1
                        // x = x1 + t*dx, y = y1 + t*dy
                        double ellipseA = outerHalfW;
                        double ellipseB = outerHalfH;
                        double x0 = x1 - outerCx;
                        double y0 = y1 - outerCy;
                        
                        double A = (dx * dx) / (ellipseA * ellipseA) + (dy * dy) / (ellipseB * ellipseB);
                        double B = 2 * (x0 * dx / (ellipseA * ellipseA) + y0 * dy / (ellipseB * ellipseB));
                        double C = (x0 * x0) / (ellipseA * ellipseA) + (y0 * y0) / (ellipseB * ellipseB) - 1.0;
                        
                        double discr = B * B - 4 * A * C;
                        if (discr >= 0)
                        {
                            double sqrtD = Math.Sqrt(discr);
                            double t1 = (-B - sqrtD) / (2 * A);
                            double t2 = (-B + sqrtD) / (2 * A);
                            
                            double t = double.MaxValue;
                            if (t1 > 1e-9 && t1 < len) t = Math.Min(t, t1);
                            if (t2 > 1e-9 && t2 < len) t = Math.Min(t, t2);
                            
                            if (t < len)
                            {
                                return (x1 + t * dx, y1 + t * dy);
                            }
                        }
                    }
                }

                // Если не нашли пересечение с внешней границей, проверяем границу острова
                // Переводим в локальные координаты острова (обратное преобразование)
                double dxIsland1 = x1 - islandCx;
                double dyIsland1 = y1 - islandCy;
                double dxIsland2 = x2 - islandCx;
                double dyIsland2 = y2 - islandCy;
                
                // Правильное обратное преобразование: поворот на -islandAngleRad
                double localX1 = dxIsland1 * islandCos + dyIsland1 * islandSin;
                double localY1 = -dxIsland1 * islandSin + dyIsland1 * islandCos;
                double localX2 = dxIsland2 * islandCos + dyIsland2 * islandSin;
                double localY2 = -dxIsland2 * islandSin + dyIsland2 * islandCos;
                
                double segDx = localX2 - localX1;
                double segDy = localY2 - localY1;
                
                var islandIntersections = new System.Collections.Generic.List<(double t, double localX, double localY)>();
                
                if (Math.Abs(segDx) > 1e-9)
                {
                    double tLeft = (-islandHalfW - localX1) / segDx;
                    if (tLeft > 1e-9 && tLeft < 1 - 1e-9)
                    {
                        double yInt = localY1 + tLeft * segDy;
                        if (Math.Abs(yInt) <= islandHalfH + 1e-6)
                            islandIntersections.Add((tLeft, -islandHalfW, yInt));
                    }
                    
                    double tRight = (islandHalfW - localX1) / segDx;
                    if (tRight > 1e-9 && tRight < 1 - 1e-9)
                    {
                        double yInt = localY1 + tRight * segDy;
                        if (Math.Abs(yInt) <= islandHalfH + 1e-6)
                            islandIntersections.Add((tRight, islandHalfW, yInt));
                    }
                }
                
                if (Math.Abs(segDy) > 1e-9)
                {
                    double tBottom = (-islandHalfH - localY1) / segDy;
                    if (tBottom > 1e-9 && tBottom < 1 - 1e-9)
                    {
                        double xInt = localX1 + tBottom * segDx;
                        if (Math.Abs(xInt) <= islandHalfW + 1e-6)
                            islandIntersections.Add((tBottom, xInt, -islandHalfH));
                    }
                    
                    double tTop = (islandHalfH - localY1) / segDy;
                    if (tTop > 1e-9 && tTop < 1 - 1e-9)
                    {
                        double xInt = localX1 + tTop * segDx;
                        if (Math.Abs(xInt) <= islandHalfW + 1e-6)
                            islandIntersections.Add((tTop, xInt, islandHalfH));
                    }
                }
                
                if (islandIntersections.Count > 0)
                {
                    islandIntersections.Sort((p1, p2) => p1.t.CompareTo(p2.t));
                    var best = islandIntersections[0];
                    // Переводим обратно в мировые координаты
                    double worldX = islandCx + best.localX * islandCos - best.localY * islandSin;
                    double worldY = islandCy + best.localX * islandSin + best.localY * islandCos;
                    return (worldX, worldY);
                }
                
                // Если не нашли пересечение, возвращаем конечную точку
                return (x2, y2);
            }

            // Функция движения по контуру внешней границы
            bool MoveAlongOuterContour(double x1, double y1, double x2, double y2, double zLevel, double retractH, double feedRapid)
            {
                if (outerType == OuterBoundaryType.Rectangle)
                {
                    // Движение по прямоугольному контуру
                    double tolerance = 1e-4;
                    double left = outerCx - outerHalfW;
                    double right = outerCx + outerHalfW;
                    double bottom = outerCy - outerHalfH;
                    double top = outerCy + outerHalfH;
                    
                    var corners = new[]
                    {
                        (left, bottom),  // 0: bottom-left
                        (right, bottom), // 1: bottom-right
                        (right, top),    // 2: top-right
                        (left, top),     // 3: top-left
                    };
                    
                    int GetSide(double x, double y)
                    {
                        if (Math.Abs(y - bottom) < tolerance) return 0;
                        if (Math.Abs(x - right) < tolerance) return 1;
                        if (Math.Abs(y - top) < tolerance) return 2;
                        if (Math.Abs(x - left) < tolerance) return 3;
                        return -1;
                    }
                    
                    int sideStart = GetSide(x1, y1);
                    int sideEnd = GetSide(x2, y2);
                    
                    if (sideStart < 0 || sideEnd < 0) return false;
                    
                    double retractZ = zLevel + retractH;
                    
                    if (sideStart == sideEnd)
                    {
                        addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                        addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                        addLine($"{g0} Z{zLevel.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                        return true;
                    }
                    
                    bool ccw = direction == MillingDirection.Clockwise;
                    int sideStep = ccw ? -1 : 1;
                    
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    
                    int currentSide = sideStart;
                    while (currentSide != sideEnd)
                    {
                        int cornerIdx = ccw ? (currentSide + 1 + 4) % 4 : currentSide;
                        addLine($"{g0} X{corners[cornerIdx].Item1.ToString(fmt, culture)} Y{corners[cornerIdx].Item2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                        currentSide = (currentSide + sideStep + 4) % 4;
                    }
                    
                    addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{zLevel.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    return true;
                }
                else // Ellipse
                {
                    // Движение по эллиптическому контуру
                    // Упрощенный подход: поднимаем, переходим, опускаем
                    double retractZ = zLevel + retractH;
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{zLevel.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                    return true;
                }
            }

            // Функция для перемещения с подъёмом инструмента
            void MoveWithRetract(double x1, double y1, double x2, double y2)
            {
                addLine($"{g0} Z{safeZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{feedZRapid.ToString(fmt, culture)}");
            }

            // Генерируем точки спирали
            var spiralPoints = new System.Collections.Generic.List<(double x, double y)>();
            spiralPoints.Add((outerCx, outerCy)); // Начинаем с центра внешней границы

            for (double θ = angleStep; θ <= θMax + 1e-9; θ += angleStep)
            {
                double r = a + b * θ;
                double ang = θ * dirSign;

                double x = outerCx + r * Math.Cos(ang);
                double y = outerCy + r * Math.Sin(ang);
                spiralPoints.Add((x, y));
            }

            // Обрабатываем точки спирали
            double prevX = outerCx, prevY = outerCy;
            bool prevInside = true;
            addLine($"{g1} X{prevX.ToString(fmt, culture)} Y{prevY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

            double exitX = 0, exitY = 0;
            bool hasExitPoint = false;

            for (int i = 1; i < spiralPoints.Count; i++)
            {
                var point = spiralPoints[i];
                double xSpiral = point.x;
                double ySpiral = point.y;
                bool currentInside = IsOnOrInsideValidArea(xSpiral, ySpiral);

                if (prevInside && currentInside)
                {
                    // Обе точки внутри - просто добавляем
                    addLine($"{g1} X{xSpiral.ToString(fmt, culture)} Y{ySpiral.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    prevX = xSpiral;
                    prevY = ySpiral;
                }
                else if (prevInside && !currentInside)
                {
                    // Выход из допустимой области - находим точку выхода
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
                    // Вход в допустимую область - находим точку входа
                    var prevPoint = spiralPoints[i - 1];
                    var entry = FindBorderIntersection(prevPoint.x, prevPoint.y, xSpiral, ySpiral);
                    
                    if (hasExitPoint)
                    {
                        // Строим путь по контуру от точки выхода до точки входа
                        if (!MoveAlongOuterContour(exitX, exitY, entry.x, entry.y, currentZ, retractHeight, feedXYRapid))
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
                    // Обе точки снаружи - обновляем предыдущую точку
                    prevX = xSpiral;
                    prevY = ySpiral;
                    prevInside = false;
                }
            }

            // Полный обход внешнего контура и контура острова
            double startX = hasExitPoint ? exitX : prevX;
            double startY = hasExitPoint ? exitY : prevY;

            // Сначала обходим внешний контур
            if (outerType == OuterBoundaryType.Rectangle)
            {
                double left = outerCx - outerHalfW;
                double right = outerCx + outerHalfW;
                double bottom = outerCy - outerHalfH;
                double top = outerCy + outerHalfH;
                
                var corners = new[]
                {
                    (left, bottom),  // 0
                    (right, bottom), // 1
                    (right, top),    // 2
                    (left, top),     // 3
                };
                
                bool clockwise = direction == MillingDirection.Clockwise;
                int[] order = clockwise ? new[] { 0, 3, 2, 1, 0 } : new[] { 0, 1, 2, 3, 0 };
                
                for (int i = 0; i < order.Length - 1; i++)
                {
                    var corner = corners[order[i]];
                    addLine($"{g1} X{corner.Item1.ToString(fmt, culture)} Y{corner.Item2.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
            }
            else // Ellipse
            {
                // Обход эллиптического контура
                int ellipsePoints = 64;
                bool clockwise = direction == MillingDirection.Clockwise;
                double angleStepEllipse = 2 * Math.PI / ellipsePoints;
                double dirSignEllipse = clockwise ? -1.0 : 1.0;
                
                for (int i = 0; i <= ellipsePoints; i++)
                {
                    double angle = i * angleStepEllipse * dirSignEllipse;
                    double x = outerCx + outerHalfW * Math.Cos(angle);
                    double y = outerCy + outerHalfH * Math.Sin(angle);
                    addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
            }

            // Затем обходим контур острова снаружи
            GenerateIslandContour(addLine, g1, fmt, culture,
                                  islandCx, islandCy, islandHalfW, islandHalfH,
                                  islandAngleRad, islandCos, islandSin,
                                  direction, feedXYWork);
        }

        /// <summary>
        /// Генерация концентрических эллипсов для области между внешней границей и островом.
        /// </summary>
        private void GenerateIslandMillingEllipses(Action<string> addLine,
                                                   string g1,
                                                   string fmt,
                                                   CultureInfo culture,
                                                   double outerCx, double outerCy,
                                                   double outerHalfW, double outerHalfH,
                                                   double islandCx, double islandCy,
                                                   double islandHalfW, double islandHalfH,
                                                   double islandAngleRad,
                                                   double islandCos, double islandSin,
                                                   double step,
                                                   MillingDirection direction,
                                                   double feedXYWork)
        {
            // Вычисляем минимальное расстояние от внешней границы до острова
            double minOuter = Math.Min(outerHalfW, outerHalfH);
            double maxIsland = Math.Max(islandHalfW, islandHalfH);
            double maxOffset = minOuter - maxIsland - 1e-6;

            if (maxOffset <= 0)
                return;

            // Генерируем концентрические эллипсы от внешней границы к острову
            var offsets = new System.Collections.Generic.List<double>();
            for (double o = 0; o <= maxOffset; o += step)
                offsets.Add(o);
            if (offsets.Count == 0 || offsets.Last() < maxOffset)
                offsets.Add(maxOffset);

            bool clockwise = direction == MillingDirection.Clockwise;
            int ellipsePoints = 64;
            double angleStep = 2 * Math.PI / ellipsePoints;
            double dirSign = clockwise ? -1.0 : 1.0;

            foreach (var offset in offsets)
            {
                var w = outerHalfW - offset;
                var h = outerHalfH - offset;

                if (w <= islandHalfW || h <= islandHalfH)
                    break;

                // Генерируем точки эллипса
                var ellipsePointsList = new System.Collections.Generic.List<(double x, double y)>();
                for (int i = 0; i <= ellipsePoints; i++)
                {
                    double angle = i * angleStep * dirSign;
                    double x = outerCx + w * Math.Cos(angle);
                    double y = outerCy + h * Math.Sin(angle);
                    
                    // Проверяем, что точка снаружи острова
                    double dxIsland = x - islandCx;
                    double dyIsland = y - islandCy;
                    // Правильное обратное преобразование: поворот на -islandAngleRad
                    double localX = dxIsland * islandCos + dyIsland * islandSin;
                    double localY = -dxIsland * islandSin + dyIsland * islandCos;
                    
                    // Точка снаружи острова, если она не внутри
                    bool insideIsland = Math.Abs(localX) < islandHalfW && Math.Abs(localY) < islandHalfH;
                    if (!insideIsland)
                    {
                        ellipsePointsList.Add((x, y));
                    }
                }

                // Генерируем траекторию по эллипсу (только точки снаружи острова)
                if (ellipsePointsList.Count > 1)
                {
                    foreach (var point in ellipsePointsList)
                    {
                        addLine($"{g1} X{point.x.ToString(fmt, culture)} Y{point.y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                    }
                }
            }
        }

        #endregion
    }
}
