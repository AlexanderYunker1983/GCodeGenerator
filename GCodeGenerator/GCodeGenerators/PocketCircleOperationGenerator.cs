using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketCircleOperationGenerator : IOperationGenerator
    {
        /// <summary>
        /// Генерирует G‑код для вырезания круглой полости.
        /// Учитывает режимы черновой/чистовой обработки и припуск.
        /// </summary>
        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var op = operation as PocketCircleOperation;
            if (op == null) return;

            bool roughing = op.IsRoughingEnabled;
            bool finishing = op.IsFinishingEnabled;
            double allowance = Math.Max(0.0, op.FinishAllowance);

            // Если оба выключены – обрабатываем как раньше, без припуска
            if (!roughing && !finishing)
            {
                roughing = true;
                allowance = 0.0;
            }

            // Черновая обработка: уменьшаем глубину и радиус на припуск
            if (roughing)
            {
                var roughOp = CloneOp(op);
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, roughOp.TotalDepth - 1e-6));

                if (depthAllowance > 0)
                {
                    roughOp.TotalDepth -= depthAllowance;
                    roughOp.Radius -= depthAllowance;

                    if (roughOp.Radius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Pocket too small after roughing allowance, skipping)");
                        return;
                    }
                }

                GenerateInternal(roughOp, addLine, g0, g1, settings);
            }

            // Чистовая обработка
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
                baseFinishOp.FinishAllowance = allowance;

                switch (op.FinishingMode)
                {
                    case PocketFinishingMode.Walls:
                        GenerateWallsFinishing(baseFinishOp, allowance, addLine, g0, g1, settings);
                        break;

                    case PocketFinishingMode.Bottom:
                        {
                            // Только дно: уменьшаем радиус на припуск и обрабатываем внутреннюю часть
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.Radius -= allowance;
                            if (bottomOp.Radius > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);
                        }
                        break;

                    case PocketFinishingMode.All:
                    default:
                        {
                            // Сначала дно, затем стенки
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.Radius -= allowance;
                            if (bottomOp.Radius > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);

                            GenerateWallsFinishing(baseFinishOp, allowance, addLine, g0, g1, settings);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Базовая генерация (как раньше), без учёта режимов rough/finish.
        /// </summary>
        private void GenerateInternal(
            PocketCircleOperation op,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;
            double step = op.ToolDiameter * (stepPercent / 100.0);
            if (step < 1e-6) step = op.ToolDiameter * 0.4;

            double baseRadius = op.Radius - toolRadius;
            if (baseRadius <= 0) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            if (op.PocketStrategy == PocketStrategy.Spiral)
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadius = baseRadius - offset;
                    if (effectiveRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    double a = 0.0;
                    double b = step / (2 * Math.PI);

                    int pointsPerRevolution = 128;
                    double stepAngle = 2 * Math.PI / pointsPerRevolution;
                    double θMax = effectiveRadius / b;

                    addLine($"{g1} X{(op.CenterX + a).ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (double θ = stepAngle; θ <= θMax; θ += stepAngle)
                    {
                        double r = a + b * θ;
                        double x = op.CenterX + r * Math.Cos(θ);
                        double y = op.CenterY + r * Math.Sin(θ);
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    double xLast = op.CenterX + effectiveRadius * Math.Cos(θMax);
                    double yLast = op.CenterY + effectiveRadius * Math.Sin(θMax);

                    addLine($"{g1} X{xLast.ToString(fmt, culture)} Y{yLast.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (int i = 0; i <= pointsPerRevolution; i++)
                    {
                        double θFull = θMax + i * stepAngle;
                        double x = op.CenterX + effectiveRadius * Math.Cos(θFull);
                        double y = op.CenterY + effectiveRadius * Math.Sin(θFull);
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else if (op.PocketStrategy == PocketStrategy.Radial)
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadius = baseRadius - offset;
                    if (effectiveRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    var lastHit = GenerateRadial(addLine, g1, fmt, culture, op, effectiveRadius, step, settings);

                    GenerateOuterCircle(addLine, g1, fmt, culture, op, effectiveRadius, lastHit);

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else if (op.PocketStrategy == PocketStrategy.Lines)
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadius = baseRadius - offset;
                    if (effectiveRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    var lastHit = GenerateLines(addLine, g0, g1, fmt, culture, op, effectiveRadius, step, nextZ, zigZag: false);

                    GenerateOuterCircle(addLine, g1, fmt, culture, op, effectiveRadius, lastHit);

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else if (op.PocketStrategy == PocketStrategy.ZigZag)
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadius = baseRadius - offset;
                    if (effectiveRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    var lastHit = GenerateLines(addLine, g0, g1, fmt, culture, op, effectiveRadius, step, nextZ, zigZag: true);

                    GenerateOuterCircle(addLine, g1, fmt, culture, op, effectiveRadius, lastHit);

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else   // Concentric
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadius = baseRadius - offset;
                    if (effectiveRadius <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    for (double r = 0; r <= effectiveRadius; r += step)
                    {
                        double radius = r;
                        int segments = Math.Max(32,
                            (int)Math.Ceiling(2 * Math.PI * radius / (op.ToolDiameter * 0.5)));
                        if (segments < 4) segments = 4;

                        double angleStep = 2 * Math.PI / segments *
                                          ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

                        double startX = op.CenterX + radius;
                        double startY = op.CenterY;
                        addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                        for (int i = 1; i <= segments; i++)
                        {
                            double ang = angleStep * i;
                            double x = op.CenterX + radius * Math.Cos(ang);
                            double y = op.CenterY + radius * Math.Sin(ang);

                            addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                        }
                    }

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
        }

        /// <summary>
        /// Чистовая обработка стенок круглого кармана:
        /// несколько радиальных проходов по стенке на всю высоту припуска.
        /// </summary>
        private void GenerateWallsFinishing(
            PocketCircleOperation op,
            double radialAllowance,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            double toolRadius = op.ToolDiameter / 2.0;
            double stepRadial = op.StepDepth;
            if (stepRadial <= 0)
                stepRadial = op.ToolDiameter * 0.25;

            double baseRadius = op.Radius - toolRadius;
            if (baseRadius <= 0) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            double startZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;

            double allowance = Math.Max(0.0, radialAllowance);
            int radialPasses = allowance > 1e-6
                ? Math.Max(1, (int)Math.Ceiling(allowance / stepRadial))
                : 1;
            double radialStep = (radialPasses > 0 && allowance > 1e-6) ? allowance / radialPasses : 0.0;

            // Радиус итоговой стенки с учётом уклона на нижней точке
            double depthFromTop = op.ContourHeight - finalZ;
            double offset = depthFromTop * taperTan;
            double effectiveRadiusFinal = baseRadius - offset;
            if (effectiveRadiusFinal <= 0)
            {
                if (settings.UseComments)
                    addLine("(Taper offset too large, stopping finishing walls)");
                return;
            }

            for (int i = 0; i < radialPasses; i++)
            {
                double remaining = allowance - (i + 1) * radialStep;
                if (remaining < 0) remaining = 0;

                double radius = effectiveRadiusFinal - remaining;
                if (radius <= 0)
                    continue;

                if (settings.UseComments)
                    addLine($"(Finishing walls radial pass {i + 1}/{radialPasses}, stock {remaining.ToString(fmt, culture)}mm)");

                // Подъём, подход к начальной точке и один проход по Z на всю высоту припуска
                double startX = op.CenterX + radius;
                double startY = op.CenterY;

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{startZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{finalZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                // Обход стенки по окружности
                GenerateOuterCircle(addLine, g1, fmt, culture, op, radius, (startX, startY));

                // Уход в центр и подъём
                addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            }
        }

        private (double x, double y) GenerateRadial(Action<string> addLine, string g1, string fmt, CultureInfo culture,
                                                    PocketCircleOperation op, double effectiveRadius, double step, GCodeSettings settings)
        {
            var circumference = 2 * Math.PI * effectiveRadius;
            var segments = Math.Max(12, (int)Math.Ceiling(circumference / step));
            var angleStep = 2 * Math.PI / segments * ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

            (double x, double y) lastHit = (op.CenterX + effectiveRadius, op.CenterY); // fallback

            for (int i = 0; i < segments; i++)
            {
                double ang1 = angleStep * i;
                double ang2 = ang1 + angleStep;

                double x1 = op.CenterX + effectiveRadius * Math.Cos(ang1);
                double y1 = op.CenterY + effectiveRadius * Math.Sin(ang1);
                double x2 = op.CenterX + effectiveRadius * Math.Cos(ang2);
                double y2 = op.CenterY + effectiveRadius * Math.Sin(ang2);

                addLine($"{g1} X{x1.ToString(fmt, culture)} Y{y1.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{x2.ToString(fmt, culture)} Y{y2.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                lastHit = (x2, y2);
            }

            return lastHit;
        }

        private (double x, double y) GenerateLines(Action<string> addLine, string g0, string g1, string fmt, CultureInfo culture,
                                                   PocketCircleOperation op, double effectiveRadius, double step, double cutZ, bool zigZag)
        {
            // Вектор направления линии и нормали
            double dirAng = op.LineAngleDeg * Math.PI / 180.0;
            double dirX = Math.Cos(dirAng);
            double dirY = Math.Sin(dirAng);
            double nx = -dirY; // нормаль (перпендикуляр)
            double ny = dirX;

            double minT = -effectiveRadius;
            double maxT = effectiveRadius;

            var offsets = new System.Collections.Generic.List<double>();
            for (double t = minT; t <= maxT + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxT - 1e-6)
                offsets.Add(maxT);

            (double x, double y) lastHit = (op.CenterX + effectiveRadius * Math.Cos(dirAng), op.CenterY + effectiveRadius * Math.Sin(dirAng));
            bool first = true;

            var segments = new System.Collections.Generic.List<(double sx, double sy, double ex, double ey, double angStart, double angEnd)>();

            foreach (var t in offsets)
            {
                double under = effectiveRadius * effectiveRadius - t * t;
                if (under < 0) continue;
                double halfChord = Math.Sqrt(under);

                double sx = op.CenterX + nx * t - dirX * halfChord;
                double sy = op.CenterY + ny * t - dirY * halfChord;
                double ex = op.CenterX + nx * t + dirX * halfChord;
                double ey = op.CenterY + ny * t + dirY * halfChord;

                double angS = Math.Atan2(sy - op.CenterY, sx - op.CenterX);
                double angE = Math.Atan2(ey - op.CenterY, ex - op.CenterX);
                segments.Add((sx, sy, ex, ey, angS, angE));
            }

            if (segments.Count == 0) return lastHit;

            // Выполнение
            // Подъём и подход только перед первой линией (в прямом направлении)
            var firstSeg = segments[0];
            var firstStartX = firstSeg.sx;
            var firstStartY = firstSeg.sy;
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{firstStartX.ToString(fmt, culture)} Y{firstStartY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{cutZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

            double prevEndAng = firstSeg.angEnd;
            bool firstLineDone = false;

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                bool reverse = zigZag && (i % 2 == 1);

                double startX = reverse ? seg.ex : seg.sx;
                double startY = reverse ? seg.ey : seg.sy;
                double endX = reverse ? seg.sx : seg.ex;
                double endY = reverse ? seg.sy : seg.ey;
                double startAng = reverse ? seg.angEnd : seg.angStart;
                double endAng = reverse ? seg.angStart : seg.angEnd;

                if (i > 0 && !zigZag)
                {
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                    addLine($"{g0} Z{cutZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                }
                else if (i > 0 && zigZag)
                {
                    // уже на глубине, переходим по дуге от предыдущего конца к новому старту
                    MoveAlongCircle(addLine, g1, fmt, culture, op, effectiveRadius, prevEndAng, startAng);
                }

                addLine($"{g1} X{endX.ToString(fmt, culture)} Y{endY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                // Переход по окружности до следующей линии, оставаясь на глубине
                if (zigZag && i + 1 < segments.Count)
                {
                    var nextSeg = segments[i + 1];
                    double nextStartAng = ((i + 1) % 2 == 1)
                        ? nextSeg.angEnd
                        : nextSeg.angStart;

                    MoveAlongCircle(addLine, g1, fmt, culture, op, effectiveRadius, endAng, nextStartAng);
                }

                lastHit = (endX, endY);
                prevEndAng = endAng;
                firstLineDone = true;
            }

            return lastHit;
        }

        private void GenerateOuterCircle(Action<string> addLine, string g1, string fmt, CultureInfo culture,
                                         PocketCircleOperation op, double effectiveRadius,
                                         (double x, double y)? startPoint = null)
        {
            int segments = Math.Max(32,
                (int)Math.Ceiling(2 * Math.PI * effectiveRadius / (op.ToolDiameter * 0.5)));
            if (segments < 4) segments = 4;

            double angleStep = 2 * Math.PI / segments *
                               ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

            double startAng = 0;
            if (startPoint.HasValue)
            {
                var p = startPoint.Value;
                startAng = Math.Atan2(p.y - op.CenterY, p.x - op.CenterX);
            }

            double startX = op.CenterX + effectiveRadius * Math.Cos(startAng);
            double startY = op.CenterY + effectiveRadius * Math.Sin(startAng);
            addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            for (int i = 1; i <= segments; i++)
            {
                double ang = startAng + angleStep * i;
                double x = op.CenterX + effectiveRadius * Math.Cos(ang);
                double y = op.CenterY + effectiveRadius * Math.Sin(ang);

                addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        private void MoveAlongCircle(Action<string> addLine, string g1, string fmt, CultureInfo culture,
                                     PocketCircleOperation op, double radius, double angStart, double angEnd)
        {
            // Нормализуем разницу углов
            double delta = angEnd - angStart;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta < -Math.PI) delta += 2 * Math.PI;

            int segs = Math.Max(12, (int)Math.Ceiling(Math.Abs(delta) * radius / (op.ToolDiameter * 0.5)));
            double step = delta / segs;

            for (int i = 1; i <= segs; i++)
            {
                double ang = angStart + step * i;
                double x = op.CenterX + radius * Math.Cos(ang);
                double y = op.CenterY + radius * Math.Sin(ang);
                addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        private PocketCircleOperation CloneOp(PocketCircleOperation src)
        {
            return new PocketCircleOperation
            {
                Name = src.Name,
                IsEnabled = src.IsEnabled,
                PocketStrategy = src.PocketStrategy,
                Direction = src.Direction,
                CenterX = src.CenterX,
                CenterY = src.CenterY,
                Radius = src.Radius,
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
