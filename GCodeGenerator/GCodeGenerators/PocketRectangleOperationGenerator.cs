using System;
using System.Globalization;
using System.Linq;
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

            double halfW = op.Width / 2.0 - toolRadius;
            double halfH = op.Height / 2.0 - toolRadius;
            if (halfW <= 0 || halfH <= 0) return;

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

                // ----------- генерация траектории ----------
                if (op.PocketStrategy == PocketStrategy.Spiral)
                    GenerateSpiral(addLine, g1,
                                   fmt, culture,
                                   cx, cy, halfW, halfH,
                                   step, angleRad,
                                   op.FeedXYWork);
                else
                    GenerateConcentricRectangles(addLine, g1,
                                                fmt, culture,
                                                cx, cy, halfW, halfH,
                                                op.Direction,
                                                step, angleRad,
                                                op.FeedXYWork);

                // Переход к безопасной высоте
                addLine($"{g0} Z{op.SafeZHeight.ToString(fmt, culture)} F{op.FeedZRapid.ToString(fmt, culture)}");

                currentZ = nextZ;
            }
        }

        #region Вспомогательные методы

        /// <summary>
        /// Генерация траектории Archimedean‑спирали,
        /// ограниченной прямоугольником (halfW × halfH).
        /// При выходе спирали за пределы прямоугольника
        /// движение продолжается по ближайшей стенке, а после окончания спирали –
        /// полный обход внешнего прямоугольника.
        /// </summary>
        private void GenerateSpiral(Action<string> addLine, string g1,
                                    string fmt, CultureInfo culture,
                                    double cx, double cy,
                                    double halfW, double halfH,
                                    double step,
                                    double angleRad,
                                    double feedXYWork)
        {
            // Максимальный радиус спирали – минимум от половин ширины/высоты
            var maxRadius = Math.Min(halfW, halfH);

            const double a = 0.0;                        // r(θ) = a + b·θ
            double b = step / (2 * Math.PI);             // радиальная скорость за один оборот

            int pointsPerRevolution = 128;
            double angleStep = 2 * Math.PI / pointsPerRevolution;

            double θMax = (maxRadius - a) / b;

            // Текущая точка – центр
            double prevX = cx, prevY = cy;
            addLine($"{g1} X{prevX.ToString(fmt, culture)} Y{prevY.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

            for (double θ = angleStep; θ <= θMax + 1e-9; θ += angleStep)
            {
                double r = a + b * θ;
                double xTmp = cx + r * Math.Cos(θ);
                double yTmp = cy + r * Math.Sin(θ);

                // Если точка вне прямоугольника – «прокладываем» к ближайшему ребру
                bool outside =
                    (xTmp < cx - halfW) || (xTmp > cx + halfW) ||
                    (yTmp < cy - halfH) || (yTmp > cy + halfH);

                double x = xTmp, y = yTmp;
                if (outside)
                {
                    // Приводим координаты к границам прямоугольника
                    x = Math.Max(cx - halfW, Math.Min(cx + halfW, x));
                    y = Math.Max(cy - halfH, Math.Min(cy + halfH, y));

                    // При переходе на стенку добавляем точку (если она отличается от предыдущей)
                    if (Math.Abs(x - prevX) > 1e-9 || Math.Abs(y - prevY) > 1e-9)
                        addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
                else
                {
                    // Внутри прямоугольника – обычный шаг спирали
                    addLine($"{g1} X{x.ToString(fmt, culture)} Y{y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }

                prevX = x;
                prevY = y;
            }

            // ----------- полный обход внешнего прямоугольника -------------
            double left = cx - halfW;
            double right = cx + halfW;
            double bottom = cy - halfH;
            double top = cy + halfH;

            addLine($"{g1} X{left.ToString(fmt, culture)} Y{bottom.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            addLine($"{g1} X{right.ToString(fmt, culture)} Y{bottom.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            addLine($"{g1} X{right.ToString(fmt, culture)} Y{top.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            addLine($"{g1} X{left.ToString(fmt, culture)} Y{top.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
            addLine($"{g1} X{left.ToString(fmt, culture)} Y{bottom.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
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
                                                  double feedXYWork)
        {
            var minHalf = Math.Min(halfW, halfH);
            var offsets = new System.Collections.Generic.List<double>();
            var maxOffset = minHalf - 1e-6;
            for (double o = 0; o <= maxOffset; o += step)
                offsets.Add(o);
            if (offsets.Count == 0 || offsets.Last() < maxOffset)
                offsets.Add(maxOffset);
            offsets = offsets.OrderByDescending(v => v).ToList(); // от внутреннего к наружному

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

                var rect = new[]
                {
                    Rot(-w, -h),
                    Rot( w, -h),
                    Rot( w,  h),
                    Rot(-w,  h),
                    Rot(-w, -h)
                };

                if (clockwise)
                    rect = new[] { rect[0], rect[3], rect[2], rect[1], rect[0] };

                // Соединяем с последней точкой
                addLine($"{g1} X{rect[0].X.ToString(fmt, culture)} Y{rect[0].Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");

                for (int i = 1; i < rect.Length; i++)
                {
                    var p = rect[i];
                    addLine($"{g1} X{p.X.ToString(fmt, culture)} Y{p.Y.ToString(fmt, culture)} F{feedXYWork.ToString(fmt, culture)}");
                }
            }
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
