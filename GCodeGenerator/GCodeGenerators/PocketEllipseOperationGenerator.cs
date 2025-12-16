using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketEllipseOperationGenerator : IOperationGenerator
    {
        /// <summary>
        /// Генерирует G‑код для вырезания эллиптической полости с учётом rough/finish.
        /// </summary>
        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,
            string g1,
            GCodeSettings settings)
        {
            var op = operation as PocketEllipseOperation;
            if (op == null) return;

            bool roughing = op.IsRoughingEnabled;
            bool finishing = op.IsFinishingEnabled;
            double allowance = Math.Max(0.0, op.FinishAllowance);

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
                    roughOp.TotalDepth -= depthAllowance;
                    roughOp.RadiusX -= depthAllowance;
                    roughOp.RadiusY -= depthAllowance;

                    if (roughOp.RadiusX <= 0 || roughOp.RadiusY <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Pocket too small after roughing allowance, skipping)");
                        return;
                    }
                }

                GenerateInternal(roughOp, addLine, g0, g1, settings);
            }

            if (finishing && allowance > 0)
            {
                double depthAllowance = Math.Min(allowance, Math.Max(0.0, op.TotalDepth));
                if (depthAllowance < 1e-6)
                    return;

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
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.RadiusX -= allowance;
                            bottomOp.RadiusY -= allowance;
                            if (bottomOp.RadiusX > 0 && bottomOp.RadiusY > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);
                        }
                        break;

                    case PocketFinishingMode.All:
                    default:
                        {
                            var bottomOp = CloneOp(baseFinishOp);
                            bottomOp.RadiusX -= allowance;
                            bottomOp.RadiusY -= allowance;
                            if (bottomOp.RadiusX > 0 && bottomOp.RadiusY > 0)
                                GenerateInternal(bottomOp, addLine, g0, g1, settings);

                            GenerateWallsFinishing(baseFinishOp, allowance, addLine, g0, g1, settings);
                        }
                        break;
                }
            }
        }

        private void GenerateInternal(
            PocketEllipseOperation op,
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

            double baseRadiusX = op.RadiusX - toolRadius;
            double baseRadiusY = op.RadiusY - toolRadius;
            if (baseRadiusX <= 0 || baseRadiusY <= 0) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            double rotationRad = op.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotationRad);
            double sinRot = Math.Sin(rotationRad);

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

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadiusX = baseRadiusX - offset;
                    double effectiveRadiusY = baseRadiusY - offset;
                    if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0)
                    {
                        if (settings.UseComments)
                            addLine("(Taper offset too large, stopping)");
                        break;
                    }

                    int pointsPerRevolution = 128;
                    double stepAngle = 2 * Math.PI / pointsPerRevolution;

                    double avgRadius = (effectiveRadiusX + effectiveRadiusY) / 2.0;
                    double b = step / (2 * Math.PI);
                    double θMax = avgRadius / b;
                    double dirSign = op.Direction == MillingDirection.Clockwise ? -1.0 : 1.0;

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (double θ = stepAngle; θ <= θMax; θ += stepAngle)
                    {
                        double r = b * θ;
                        double t = θ * dirSign;

                        double aEllipse = effectiveRadiusX * (r / avgRadius);
                        double bEllipse = effectiveRadiusY * (r / avgRadius);
                        double xEllipse = aEllipse * Math.Cos(t);
                        double yEllipse = bEllipse * Math.Sin(t);

                        double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                        double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    for (int i = 0; i <= pointsPerRevolution; i++)
                    {
                        double t = i * stepAngle * dirSign;
                        double xEllipse = effectiveRadiusX * Math.Cos(t);
                        double yEllipse = effectiveRadiusY * Math.Sin(t);

                        double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                        double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    // Возврат в центр на холостом ходу с подъемом
                    double retractZ = nextZ + op.RetractHeight;
                    addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
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
                    double effectiveRadiusX = baseRadiusX - offset;
                    double effectiveRadiusY = baseRadiusY - offset;
                    if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0)
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

                    var lastHit = GenerateRadial(addLine, g0, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, step, nextZ, settings);

                    GenerateOuterEllipse(addLine, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, lastHit);

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
                    double effectiveRadiusX = baseRadiusX - offset;
                    double effectiveRadiusY = baseRadiusY - offset;
                    if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0)
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

                    var lastHit = GenerateLines(addLine, g0, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, step, nextZ);

                    GenerateOuterEllipse(addLine, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, lastHit);

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
                    double effectiveRadiusX = baseRadiusX - offset;
                    double effectiveRadiusY = baseRadiusY - offset;
                    if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0)
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

                    var lastHit = GenerateLines(addLine, g0, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, step, nextZ, zigZag: true);

                    GenerateOuterEllipse(addLine, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, lastHit);

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    double depthFromTop = op.ContourHeight - nextZ;
                    double offset = depthFromTop * taperTan;
                    double effectiveRadiusX = baseRadiusX - offset;
                    double effectiveRadiusY = baseRadiusY - offset;
                    if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0)
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

                    double maxEffectiveRadius = Math.Max(effectiveRadiusX, effectiveRadiusY);
                    for (double r = 0; r <= maxEffectiveRadius; r += step)
                    {
                        double scaleX = r / maxEffectiveRadius;
                        double scaleY = r / maxEffectiveRadius;
                        double currentRadiusX = effectiveRadiusX * scaleX;
                        double currentRadiusY = effectiveRadiusY * scaleY;

                        double perimeter = Math.PI * (3 * (currentRadiusX + currentRadiusY) - Math.Sqrt((3 * currentRadiusX + currentRadiusY) * (currentRadiusX + 3 * currentRadiusY)));
                        int segments = Math.Max(32, (int)Math.Ceiling(perimeter / (op.ToolDiameter * 0.5)));
                        if (segments < 4) segments = 4;

                        double angleStep = 2 * Math.PI / segments *
                                          ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

                        double startXEllipse = currentRadiusX;
                        double startYEllipse = 0;
                        double startX = op.CenterX + startXEllipse * cosRot - startYEllipse * sinRot;
                        double startY = op.CenterY + startXEllipse * sinRot + startYEllipse * cosRot;
                        addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                        for (int i = 1; i <= segments; i++)
                        {
                            double t = angleStep * i;
                            double xEllipse = currentRadiusX * Math.Cos(t);
                            double yEllipse = currentRadiusY * Math.Sin(t);

                            double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                            double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                            addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                        }
                    }

                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
        }

        private void GenerateWallsFinishing(
            PocketEllipseOperation op,
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

            double baseRadiusX = op.RadiusX - toolRadius;
            double baseRadiusY = op.RadiusY - toolRadius;
            if (baseRadiusX <= 0 || baseRadiusY <= 0) return;

            var taperAngleRad = op.WallTaperAngleDeg * Math.PI / 180.0;
            var taperTan = Math.Tan(taperAngleRad);

            double startZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;

            double allowance = Math.Max(0.0, radialAllowance);
            int radialPasses = allowance > 1e-6
                ? Math.Max(1, (int)Math.Ceiling(allowance / stepRadial))
                : 1;
            double radialStep = (radialPasses > 0 && allowance > 1e-6) ? allowance / radialPasses : 0.0;

            double rotationRad = op.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotationRad);
            double sinRot = Math.Sin(rotationRad);

            double depthFromTop = op.ContourHeight - finalZ;
            double offset = depthFromTop * taperTan;
            double effRxFinal = baseRadiusX - offset;
            double effRyFinal = baseRadiusY - offset;
            if (effRxFinal <= 0 || effRyFinal <= 0)
            {
                if (settings.UseComments)
                    addLine("(Taper offset too large, stopping finishing walls)");
                return;
            }

            for (int i = 0; i < radialPasses; i++)
            {
                double remaining = allowance - (i + 1) * radialStep;
                if (remaining < 0) remaining = 0;

                double rx = effRxFinal - remaining;
                double ry = effRyFinal - remaining;
                if (rx <= 0 || ry <= 0)
                    continue;

                if (settings.UseComments)
                    addLine($"(Finishing ellipse walls radial pass {i + 1}/{radialPasses}, stock {remaining.ToString(fmt, culture)}mm)");

                double startX = op.CenterX + rx * cosRot;
                double startY = op.CenterY + rx * sinRot;

                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{startZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g1} Z{finalZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                GenerateOuterEllipse(addLine, g1, fmt, culture, op, rx, ry, (startX, startY));

                addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            }
        }

        private (double x, double y) GenerateRadial(Action<string> addLine, string g0, string g1, string fmt, CultureInfo culture,
                                    PocketEllipseOperation op, double effRx, double effRy, double step, double currentZ, GCodeSettings settings)
        {
            // Оценка периметра эллипса
            double h = Math.Pow(effRx - effRy, 2) / Math.Pow(effRx + effRy, 2);
            double perimeter = Math.PI * (effRx + effRy) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));

            int segments = Math.Max(16, (int)Math.Ceiling(perimeter / step));
            double angleStep = 2 * Math.PI / segments * ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

            double cosRot = Math.Cos(op.RotationAngle * Math.PI / 180.0);
            double sinRot = Math.Sin(op.RotationAngle * Math.PI / 180.0);

            (double x, double y) PointOnEllipse(double ang)
            {
                // Радиус до границы по направлению ang (без поворота)
                double denom = Math.Sqrt(Math.Pow(Math.Cos(ang) / effRx, 2) + Math.Pow(Math.Sin(ang) / effRy, 2));
                double r = denom < 1e-9 ? 0 : 1.0 / denom;
                double x = r * Math.Cos(ang);
                double y = r * Math.Sin(ang);
                // Поворот
                double xr = x * cosRot - y * sinRot + op.CenterX;
                double yr = x * sinRot + y * cosRot + op.CenterY;
                return (xr, yr);
            }

            (double x, double y) lastHit = (op.CenterX + effRx * cosRot, op.CenterY + effRx * sinRot); // fallback

            for (int i = 0; i < segments; i++)
            {
                double ang1 = angleStep * i;
                double ang2 = ang1 + angleStep;

                var p1 = PointOnEllipse(ang1);
                var p2 = PointOnEllipse(ang2);

                addLine($"{g1} X{p1.x.ToString(fmt, culture)} Y{p1.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                addLine($"{g1} X{p2.x.ToString(fmt, culture)} Y{p2.y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                
                // Переход в центр на холостом ходу с подъемом
                double retractZ = currentZ + op.RetractHeight;
                addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
                addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                lastHit = p2;
            }

            return lastHit;
        }

        private (double x, double y) GenerateLines(Action<string> addLine, string g0, string g1, string fmt, CultureInfo culture,
                                    PocketEllipseOperation op, double effRx, double effRy, double step, double cutZ, bool zigZag = false)
        {
            // В локальных координатах эллипса (с учётом RotationAngle)
            double rot = op.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rot);
            double sinRot = Math.Sin(rot);

            double dirAng = op.LineAngleDeg * Math.PI / 180.0 - rot; // локальный угол
            double dirX = Math.Cos(dirAng);
            double dirY = Math.Sin(dirAng);
            double nx = -dirY;
            double ny = dirX;

            double maxOffset = Math.Max(effRx, effRy);
            var offsets = new System.Collections.Generic.List<double>();
            for (double t = -maxOffset; t <= maxOffset + 1e-9; t += step)
                offsets.Add(t);
            if (offsets.Count == 0 || offsets[offsets.Count - 1] < maxOffset - 1e-6)
                offsets.Add(maxOffset);

            var segments = new System.Collections.Generic.List<(double sx, double sy, double ex, double ey, double angStart, double angEnd)>();
            (double x, double y) lastHit = (op.CenterX + effRx * cosRot, op.CenterY + effRx * sinRot); // fallback

            foreach (var t in offsets)
            {
                double A = (dirX * dirX) / (effRx * effRx) + (dirY * dirY) / (effRy * effRy);
                double B = 2 * (dirX * nx * t / (effRx * effRx) + dirY * ny * t / (effRy * effRy));
                double C = (nx * nx * t * t) / (effRx * effRx) + (ny * ny * t * t) / (effRy * effRy) - 1;

                double disc = B * B - 4 * A * C;
                if (disc < 0) continue;
                double sqrtD = Math.Sqrt(disc);
                double s1 = (-B - sqrtD) / (2 * A);
                double s2 = (-B + sqrtD) / (2 * A);

                double sxLocal = nx * t + dirX * s1;
                double syLocal = ny * t + dirY * s1;
                double exLocal = nx * t + dirX * s2;
                double eyLocal = ny * t + dirY * s2;

                (double wx, double wy) ToWorld(double lx, double ly)
                    => (op.CenterX + lx * cosRot - ly * sinRot,
                        op.CenterY + lx * sinRot + ly * cosRot);

                var sWorld = ToWorld(sxLocal, syLocal);
                var eWorld = ToWorld(exLocal, eyLocal);

                double angS = Math.Atan2(syLocal / effRy, sxLocal / effRx);
                double angE = Math.Atan2(eyLocal / effRy, exLocal / effRx);

                segments.Add((sWorld.wx, sWorld.wy, eWorld.wx, eWorld.wy, angS, angE));
            }

            if (segments.Count == 0) return lastHit;

            // Подъём и подход только перед первой линией
            var firstSeg = segments[0];
            double firstStartX = firstSeg.sx;
            double firstStartY = firstSeg.sy;
            addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
            addLine($"{g0} X{firstStartX.ToString(fmt, culture)} Y{firstStartY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
            addLine($"{g0} Z{cutZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

            double prevEndAng = firstSeg.angEnd;

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
                    MoveAlongEllipse(addLine, g0, g1, fmt, culture, op, effRx, effRy, prevEndAng, startAng, cutZ);
                }

                addLine($"{g1} X{endX.ToString(fmt, culture)} Y{endY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                if (zigZag && i + 1 < segments.Count)
                {
                    var nextSeg = segments[i + 1];
                    double nextStartAng = ((i + 1) % 2 == 1) ? nextSeg.angEnd : nextSeg.angStart;
                    MoveAlongEllipse(addLine, g0, g1, fmt, culture, op, effRx, effRy, endAng, nextStartAng, cutZ);
                }

                lastHit = (endX, endY);
                prevEndAng = endAng;
            }

            return lastHit;
        }

        private void GenerateOuterEllipse(Action<string> addLine, string g1, string fmt, CultureInfo culture,
                                          PocketEllipseOperation op, double effRx, double effRy,
                                          (double x, double y)? startPoint = null)
        {
            double h = Math.Pow(effRx - effRy, 2) / Math.Pow(effRx + effRy, 2);
            double perimeter = Math.PI * (effRx + effRy) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            int segments = Math.Max(32, (int)Math.Ceiling(perimeter / (op.ToolDiameter * 0.5)));
            if (segments < 8) segments = 8;

            double angleStep = 2 * Math.PI / segments * ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);
            double cosRot = Math.Cos(op.RotationAngle * Math.PI / 180.0);
            double sinRot = Math.Sin(op.RotationAngle * Math.PI / 180.0);

            double startAng = 0;
            if (startPoint.HasValue)
            {
                var p = startPoint.Value;
                // повернём в локальные координаты эллипса
                double xl = (p.x - op.CenterX) * cosRot + (p.y - op.CenterY) * sinRot;
                double yl = -(p.x - op.CenterX) * sinRot + (p.y - op.CenterY) * cosRot;
                startAng = Math.Atan2(yl / effRy, xl / effRx);
            }

            double startX = op.CenterX + effRx * Math.Cos(startAng) * cosRot - effRy * Math.Sin(startAng) * sinRot;
            double startY = op.CenterY + effRx * Math.Cos(startAng) * sinRot + effRy * Math.Sin(startAng) * cosRot;
            addLine($"{g1} X{startX.ToString(fmt, culture)} Y{startY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

            for (int i = 1; i <= segments; i++)
            {
                double t = startAng + angleStep * i;
                double xEllipse = effRx * Math.Cos(t);
                double yEllipse = effRy * Math.Sin(t);

                double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
            }
        }

        private void MoveAlongEllipse(Action<string> addLine, string g0, string g1, string fmt, CultureInfo culture,
                                      PocketEllipseOperation op, double effRx, double effRy,
                                      double angStart, double angEnd, double currentZ)
        {
            double rot = op.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rot);
            double sinRot = Math.Sin(rot);

            double delta = angEnd - angStart;
            while (delta > Math.PI) delta -= 2 * Math.PI;
            while (delta < -Math.PI) delta += 2 * Math.PI;

            // Оценка длины дуги через псевдо-периметр
            double h = Math.Pow(effRx - effRy, 2) / Math.Pow(effRx + effRy, 2);
            double per = Math.PI * (effRx + effRy) * (1 + 3 * h / (10 + Math.Sqrt(4 - 3 * h)));
            int segs = Math.Max(12, (int)Math.Ceiling(Math.Abs(delta) / (2 * Math.PI) * per / (op.ToolDiameter * 0.5)));
            double step = delta / segs;

            // Поднимаем инструмент для перехода
            double retractZ = currentZ + op.RetractHeight;
            addLine($"{g0} Z{retractZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

            // Перемещаемся по эллипсу на холостом ходу
            for (int i = 1; i <= segs; i++)
            {
                double t = angStart + step * i;
                double xEllipse = effRx * Math.Cos(t);
                double yEllipse = effRy * Math.Sin(t);

                double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                addLine($"{g0} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");
            }

            // Опускаем обратно на рабочую высоту
            addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
        }

        private PocketEllipseOperation CloneOp(PocketEllipseOperation src)
        {
            return new PocketEllipseOperation
            {
                Name = src.Name,
                IsEnabled = src.IsEnabled,
                PocketStrategy = src.PocketStrategy,
                Direction = src.Direction,
                CenterX = src.CenterX,
                CenterY = src.CenterY,
                RadiusX = src.RadiusX,
                RadiusY = src.RadiusY,
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

