using System;
using System.Globalization;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class PocketCircleOperationGenerator : IOperationGenerator
    {
        /// <summary>
        /// Генерирует G‑код для вырезания круглой полости.
        /// В зависимости от PocketStrategy генерируется либо спираль,
        /// либо концентрические круги (стандартная схема).
        /// </summary>
        public void Generate(
            OperationBase operation,
            Action<string> addLine,
            string g0,          // команда «переход» (обычно G0)
            string g1,          // команда «работа»   (обычно G1)
            GCodeSettings settings)
        {
            var op = operation as PocketCircleOperation;
            if (op == null) return;

            // Формат вывода
            var fmt = $"0.{new string('0', op.Decimals)}";
            var culture = CultureInfo.InvariantCulture;

            // Общие параметры фрезы и глубины
            double toolRadius = op.ToolDiameter / 2.0;
            double stepPercent = (op.StepPercentOfTool <= 0) ? 40 : op.StepPercentOfTool;   // %
            double step = op.ToolDiameter * (stepPercent / 100.0);                     // толщина спирали
            if (step < 1e-6) step = op.ToolDiameter * 0.4;

            double effectiveRadius = op.Radius - toolRadius;
            if (effectiveRadius <= 0) return;      // фреза слишком крупная

            double currentZ = op.ContourHeight;
            double finalZ = op.ContourHeight - op.TotalDepth;
            int pass = 0;

            /* ------------------------------------------------------------------ */
            /* -------------------- Выбор стратегии обработки --------------------- */

            if (op.PocketStrategy == PocketStrategy.Spiral)
            {
                // ---------- Спираль ----------
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

                    // Спираль Архимеда
                    double a = 0.0;
                    double b = step / (2 * Math.PI);

                    int pointsPerRevolution = 128;
                    double stepAngle = 2 * Math.PI / pointsPerRevolution;
                    double θMax = effectiveRadius / b;

                    // Начальная точка спирали
                    addLine($"{g1} X{(op.CenterX + a).ToString(fmt, culture)} Y{op.CenterY.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");

                    for (double θ = stepAngle; θ <= θMax; θ += stepAngle)
                    {
                        double r = a + b * θ;
                        double x = op.CenterX + r * Math.Cos(θ);
                        double y = op.CenterY + r * Math.Sin(θ);

                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{op.FeedXYWork.ToString(fmt, culture)}");
                    }

                    // Завершающий круг по внешней границе
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

                    // Переход к безопасной высоте
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }
            else   /* PocketStrategy.Concentric – классический алгоритм */
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

                    // ---------- Концентрические окружности ----------
                    for (double r = 0; r <= effectiveRadius; r += step)
                    {
                        double radius = r;
                        int segments = Math.Max(32,
                            (int)Math.Ceiling(2 * Math.PI * radius / (op.ToolDiameter * 0.5)));
                        if (segments < 4) segments = 4;

                        double angleStep = 2 * Math.PI / segments *
                                          ((op.Direction == MillingDirection.Clockwise) ? -1 : 1);

                        // Начальная точка окружности
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

                    // Переход к безопасной высоте
                    addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                    currentZ = nextZ;
                }
            }   /* конец if‑else стратегии */
        }
    }
}
