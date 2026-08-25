using System;
using System.Collections.Generic;
using System.Globalization;
using GCodeGenerator.Geometry;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общие проверки параметров операций.
    ///
    /// Проверяется то, что физически невозможно выполнить на станке: нулевая
    /// рабочая подача (инструмент никуда не поедет), неположительная глубина
    /// или диаметр, шаг выборки больше диаметра фрезы (между проходами
    /// остаётся нетронутый материал), число знаков после запятой вне
    /// разумного предела, нечисловые координаты. Раньше проверялись только
    /// глубина, шаг и диаметр — остальное доходило до G-code как есть.
    /// </summary>
    public static class OperationValidation
    {
        /// <summary>
        /// Tolerance for closed-contour checks. Matches the DXF importer's
        /// closedness tolerance (0.001) so contours imported by the app are
        /// never reported as open.
        /// </summary>
        public const double ContourClosedTolerance = GeometryTolerances.PointCoincidence;

        /// <summary>Наибольшее осмысленное число знаков после запятой.</summary>
        public const int MaxDecimals = 6;

        private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Adds an issue if <paramref name="value"/> is non-finite or not greater than zero.
        /// </summary>
        public static void AddIfNotPositive(IList<ValidationIssue> issues, string property, double value)
        {
            if (!double.IsFinite(value) || value <= 0)
                issues.Add(new ValidationIssue(property, ValidationCode.NotPositive,
                    $"must be finite and greater than zero, but is {Text(value)}"));
        }

        /// <summary>Значение должно быть конечным числом (координата, высота).</summary>
        public static void AddIfNotFinite(IList<ValidationIssue> issues, string property, double value)
        {
            if (!double.IsFinite(value))
                issues.Add(new ValidationIssue(property, ValidationCode.NotFinite,
                    $"must be a finite number, but is {Text(value)}"));
        }

        /// <summary>Значение не может быть отрицательным (припуск, расстояние).</summary>
        public static void AddIfNegative(IList<ValidationIssue> issues, string property, double value)
        {
            if (!double.IsFinite(value))
            {
                AddIfNotFinite(issues, property, value);
                return;
            }

            if (value < 0)
                issues.Add(new ValidationIssue(property, ValidationCode.Negative,
                    $"must not be negative, but is {Text(value)}", 0));
        }

        /// <summary>
        /// Adds an issue if <paramref name="value"/> is below <paramref name="min"/>.
        /// </summary>
        public static void AddIfBelow(IList<ValidationIssue> issues, string property, int value, int min)
        {
            if (value < min)
                issues.Add(new ValidationIssue(property, ValidationCode.BelowMinimum,
                    $"must be at least {min}, but is {value}", min));
        }

        /// <summary>Целое значение должно попадать в диапазон.</summary>
        public static void AddIfOutOfRange(IList<ValidationIssue> issues, string property, int value, int min, int max)
        {
            if (value < min)
                issues.Add(new ValidationIssue(property, ValidationCode.BelowMinimum,
                    $"must be at least {min}, but is {value}", min));
            else if (value > max)
                issues.Add(new ValidationIssue(property, ValidationCode.AboveMaximum,
                    $"must be at most {max}, but is {value}", max));
        }

        /// <summary>Вещественное значение должно быть больше нуля и не больше предела.</summary>
        public static void AddIfOutOfRange(IList<ValidationIssue> issues, string property, double value, double min, double max)
        {
            if (!double.IsFinite(value))
            {
                AddIfNotFinite(issues, property, value);
                return;
            }

            if (value < min)
                issues.Add(new ValidationIssue(property, ValidationCode.BelowMinimum,
                    $"must be at least {Text(min)}, but is {Text(value)}", min));
            else if (value > max)
                issues.Add(new ValidationIssue(property, ValidationCode.AboveMaximum,
                    $"must be at most {Text(max)}, but is {Text(value)}", max));
        }

        /// <summary>
        /// Adds issues for the common milling parameters: TotalDepth, StepDepth
        /// and ToolDiameter must all be greater than zero.
        /// </summary>
        public static void AddCommonMillingIssues(IList<ValidationIssue> issues, double totalDepth, double stepDepth, double toolDiameter)
        {
            AddIfNotPositive(issues, "TotalDepth", totalDepth);
            AddIfNotPositive(issues, "StepDepth", stepDepth);
            AddIfNotPositive(issues, "ToolDiameter", toolDiameter);
        }

        /// <summary>
        /// Полный набор проверок фрезерной операции: параметры резания,
        /// подачи, высоты и точность вывода.
        ///
        /// Нулевая рабочая подача — не мелочь: <c>F0</c> означает, что
        /// инструмент никуда не поедет, а стойка при этом либо остановится
        /// с ошибкой, либо будет ждать бесконечно.
        /// </summary>
        public static void AddMillingIssues(IList<ValidationIssue> issues, MillingOperationBase operation)
        {
            AddCommonMillingIssues(issues, operation.TotalDepth, operation.StepDepth, operation.ToolDiameter);

            AddIfNotPositive(issues, nameof(operation.FeedXYWork), operation.FeedXYWork);
            AddIfNotPositive(issues, nameof(operation.FeedZWork), operation.FeedZWork);
            AddIfNotPositive(issues, nameof(operation.FeedXYRapid), operation.FeedXYRapid);
            AddIfNotPositive(issues, nameof(operation.FeedZRapid), operation.FeedZRapid);

            EnumValidation.AddIfUndefined(issues, nameof(operation.Direction), operation.Direction);

            AddIfNotFinite(issues, nameof(operation.ContourHeight), operation.ContourHeight);
            AddIfNotFinite(issues, nameof(operation.SafeZHeight), operation.SafeZHeight);
            AddIfNegative(issues, nameof(operation.RetractHeight), operation.RetractHeight);

            AddIfOutOfRange(issues, nameof(operation.Decimals), operation.Decimals, 0, MaxDecimals);
        }

        /// <summary>
        /// Проверки, общие для карманов: шаг выборки и припуск.
        ///
        /// Шаг больше диаметра фрезы оставляет между проходами нетронутый
        /// материал — карман получится не выбранным, а расчерченным.
        /// </summary>
        public static void AddPocketIssues(IList<ValidationIssue> issues, PocketOperationBase operation)
        {
            AddMillingIssues(issues, operation);

            EnumValidation.AddIfUndefined(issues, nameof(operation.PocketStrategy), operation.PocketStrategy);
            EnumValidation.AddIfUndefined(issues, nameof(operation.FinishingMode), operation.FinishingMode);

            AddIfOutOfRange(issues, nameof(operation.StepPercentOfTool), operation.StepPercentOfTool, 1, 100);
            AddIfNegative(issues, nameof(operation.FinishAllowance), operation.FinishAllowance);
            AddIfNotFinite(issues, nameof(operation.LineAngleDeg), operation.LineAngleDeg);

            if (operation.IsFinishingEnabled && operation.FinishAllowance <= 0)
            {
                issues.Add(new ValidationIssue(nameof(operation.FinishAllowance), ValidationCode.NotPositive,
                    "finishing pass needs a positive allowance to remove"));
            }
        }

        /// <summary>
        /// Проверки, общие для контуров: способ врезания и точность
        /// аппроксимации дуг.
        /// </summary>
        public static void AddProfileIssues(IList<ValidationIssue> issues, ProfileOperationBase operation)
        {
            AddMillingIssues(issues, operation);

            EnumValidation.AddIfUndefined(issues, nameof(operation.ToolPathMode), operation.ToolPathMode);
            EnumValidation.AddIfUndefined(issues, nameof(operation.EntryMode), operation.EntryMode);

            AddIfNotPositive(issues, nameof(operation.MaxSegmentLength), operation.MaxSegmentLength);
            AddIfNegative(issues, nameof(operation.SafeDistanceBetweenPasses), operation.SafeDistanceBetweenPasses);

            // Угол врезания важен только для наклонного входа: при нулевом
            // рампа не опускается, при прямом — это уже вертикальный вход.
            if (operation.EntryMode == EntryMode.Angled)
                AddIfOutOfRange(issues, nameof(operation.EntryAngle), operation.EntryAngle, 0.1, 89.9);
        }

        /// <summary>
        /// True if the first and last points of the contour coincide within
        /// <see cref="ContourClosedTolerance"/>.
        /// </summary>
        public static bool IsContourClosed(DxfPolyline contour)
        {
            if (contour?.Points == null || contour.Points.Count < 2)
                return false;

            var first = contour.Points[0];
            var last = contour.Points[contour.Points.Count - 1];
            if (first == null || last == null)
                return false;

            double dx = first.X - last.X;
            double dy = first.Y - last.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= ContourClosedTolerance;
        }
    }
}
