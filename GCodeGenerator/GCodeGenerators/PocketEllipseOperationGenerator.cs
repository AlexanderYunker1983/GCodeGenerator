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
    }
}

