using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketEllipseOperationGenerator : IOperationGenerator
    {
        /// <summary>
        /// Генерирует G‑код для вырезания эллиптической полости.
        /// В зависимости от PocketStrategy генерируется либо спираль,
        /// либо концентрические эллипсы (стандартная схема).
        /// </summary>
        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,          // команда «переход» (обычно G0)
            string g1,          // команда «работа»   (обычно G1)
            GCodeSettings settings)
        {
            var op = operation as PocketEllipseOperation;
            if (op == null) return;

            // Формат вывода
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // Общие параметры фрезы и глубины
            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;   // %
            double step = op.ToolDiameter * (stepPercent / 100.0);                     // толщина спирали
            if (step < 1e-6) step = op.ToolDiameter * 0.4;

            // Эффективные радиусы с учетом радиуса фрезы
            double effectiveRadiusX = op.RadiusX - toolRadius;
            double effectiveRadiusY = op.RadiusY - toolRadius;
            if (effectiveRadiusX <= 0 || effectiveRadiusY <= 0) return;      // фреза слишком крупная

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            // Угол поворота в радианах
            double rotationRad = op.RotationAngle * Math.PI / 180.0;
            double cosRot = Math.Cos(rotationRad);
            double sinRot = Math.Sin(rotationRad);

            /* ------------------------------------------------------------------ */
            /* -------------------- Выбор стратегии обработки --------------------- */

            if (op.PocketStrategy == PocketStrategy.Spiral)
            {
                // ---------- Спираль для эллипса ----------
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    // Переходы в безопасную высоту и центр
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    // Понижение на текущую глубину и начало резки
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    // Спираль Архимеда для эллипса
                    // Используем параметрическое уравнение эллипса: x = a*cos(t), y = b*sin(t)
                    // Спираль: увеличиваем a и b пропорционально углу
                    double maxRadius = Math.Max(effectiveRadiusX, effectiveRadiusY);
                    double minRadius = Math.Min(effectiveRadiusX, effectiveRadiusY);
                    
                    int pointsPerRevolution = 128;
                    double stepAngle = 2 * Math.PI / pointsPerRevolution;
                    
                    // Максимальный угол для достижения внешней границы
                    // Используем средний радиус для оценки
                    double avgRadius = (effectiveRadiusX + effectiveRadiusY) / 2.0;
                    double b = step / (2 * Math.PI);
                    double θMax = avgRadius / b;

                    // Начальная точка спирали (в центре)
                    addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (double θ = stepAngle; θ <= θMax; θ += stepAngle)
                    {
                        // Радиус спирали
                        double r = b * θ;
                        
                        // Параметрический угол для эллипса (0..2π)
                        double t = θ;
                        
                        // Координаты на эллипсе (без поворота)
                        double aEllipse = effectiveRadiusX * (r / avgRadius);
                        double bEllipse = effectiveRadiusY * (r / avgRadius);
                        double xEllipse = aEllipse * Math.Cos(t);
                        double yEllipse = bEllipse * Math.Sin(t);
                        
                        // Применяем поворот и сдвиг к центру
                        double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                        double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    // Завершающий эллипс по внешней границе
                    for (int i = 0; i <= pointsPerRevolution; i++)
                    {
                        double t = i * stepAngle;
                        double xEllipse = effectiveRadiusX * Math.Cos(t);
                        double yEllipse = effectiveRadiusY * Math.Sin(t);
                        
                        double x = op.CenterX + xEllipse * cosRot - yEllipse * sinRot;
                        double y = op.CenterY + xEllipse * sinRot + yEllipse * cosRot;

                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    // Переход к безопасной высоте
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

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    var lastHit = GenerateRadial(addLine, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, step, settings);

                    // Завершающий полный проход по контуру
                    GenerateOuterEllipse(addLine, g1, fmt, culture, op, effectiveRadiusX, effectiveRadiusY, lastHit);

                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else   /* PocketStrategy.Concentric – концентрические эллипсы */
            {
                while (currentZ > finalZ)
                {
                    double nextZ = currentZ - op.StepDepth;
                    if (nextZ < finalZ) nextZ = finalZ;
                    pass++;

                    if (settings.UseComments)
                        addLine($"(Pass {pass}, depth {nextZ.ToString(fmt, culture)})");

                    // Переходы в безопасную высоту и центр
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g0} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYRapid.ToString(fmt, culture)}");

                    // Понижение на текущую глубину и начало резки
                    addLine($"{g0} Z{currentZ.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");
                    addLine($"{g1} Z{nextZ.ToString(fmt, culture)} F{op.FeedZWork.ToString(fmt, culture)}");

                    // ---------- Концентрические эллипсы ----------
                    double maxEffectiveRadius = Math.Max(effectiveRadiusX, effectiveRadiusY);
                    for (double r = 0; r <= maxEffectiveRadius; r += step)
                    {
                        // Масштабируем радиусы пропорционально
                        double scaleX = r / maxEffectiveRadius;
                        double scaleY = r / maxEffectiveRadius;
                        double currentRadiusX = effectiveRadiusX * scaleX;
                        double currentRadiusY = effectiveRadiusY * scaleY;
                        
                        // Количество сегментов зависит от размера эллипса
                        double perimeter = Math.PI * (3 * (currentRadiusX + currentRadiusY) - Math.Sqrt((3 * currentRadiusX + currentRadiusY) * (currentRadiusX + 3 * currentRadiusY)));
                        int segments = Math.Max(32, (int)Math.Ceiling(perimeter / (op.ToolDiameter * 0.5)));
                        if (segments < 4) segments = 4;

                        double angleStep = 2 * Math.PI / segments *
                                          ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

                        // Начальная точка эллипса (на оси X)
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

                    // Переход к безопасной высоте
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }   /* конец if‑else стратегии */
        }

        private (double x, double y) GenerateRadial(Action<string> addLine, string g1, string fmt, CultureInfo culture,
                                    PocketEllipseOperation op, double effRx, double effRy, double step, GCodeSettings settings)
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
                addLine($"{g1} X{op.CenterX.ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                lastHit = p2;
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
    }
}

