#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
    ///
    /// У подач есть и верхний предел. Он поставлен не по паспорту станка —
    /// его продукт не знает, — а по тому, чего не бывает: подача с лишним
    /// разрядом. Такое значение стойка обычно урежет до своего максимума,
    /// то есть выполнит не то, что записано в проекте, и молча.
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

        /// <summary>
        /// Наибольшая рабочая подача, мм/мин.
        ///
        /// Предел ловит не станок, а лишний разряд: 3000 вместо 300 — самая
        /// частая опечатка ввода, и она уходила в программу молча. Рабочая
        /// подача выше двадцати метров в минуту не встречается даже на
        /// высокоскоростной обработке, поэтому потолок никому не мешает,
        /// а сдвиг разряда в трёхзначном значении отсекает.
        /// </summary>
        public const double MaxWorkFeed = 20000.0;

        /// <summary>
        /// Наибольшая подача быстрого хода, мм/мин.
        ///
        /// Втрое выше рабочей: холостые перемещения и правда идут в разы
        /// быстрее, и шестьдесят метров в минуту — предел быстрого хода
        /// самых быстрых станков, а не рабочий режим.
        /// </summary>
        public const double MaxRapidFeed = 60000.0;

        private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);

        private static bool HasIssue(IList<ValidationIssue> issues, string property)
            => issues.Any(issue => issue != null && issue.Property == property);

        /// <summary>
        /// Adds an issue if <paramref name="value"/> is non-finite or not greater than zero.
        /// </summary>
        public static void AddIfNotPositive(IList<ValidationIssue> issues, string property, double value)
        {
            if ((!double.IsFinite(value) || value <= 0) && !HasIssue(issues, property))
                issues.Add(new ValidationIssue(property, ValidationCode.NotPositive,
                    $"must be finite and greater than zero, but is {Text(value)}"));
        }

        /// <summary>Значение должно быть конечным числом (координата, высота).</summary>
        public static void AddIfNotFinite(IList<ValidationIssue> issues, string property, double value)
        {
            if (!double.IsFinite(value) && !HasIssue(issues, property))
                issues.Add(new ValidationIssue(property, ValidationCode.NotFinite,
                    $"must be a finite number, but is {Text(value)}"));
        }

        /// <summary>
        /// Проверяет все публичные числовые и enum-свойства операции.
        /// Геометрические параметры объявлены в конкретных наследниках, и
        /// ручной перечень общих полей неизбежно пропускал очередную
        /// координату или угол. Отражение выполняется только предполётно, а
        /// не во внутренних циклах построения траектории.
        /// </summary>
        public static void AddPublicValueIssues(IList<ValidationIssue> issues, OperationBase operation)
        {
            foreach (var property in operation.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    continue;

                if (property.PropertyType == typeof(double))
                {
                    AddIfNotFinite(issues, property.Name, (double)property.GetValue(operation)!);
                    continue;
                }

                var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (!enumType.IsEnum)
                    continue;

                var value = property.GetValue(operation);
                if (value != null && !Enum.IsDefined(enumType, value) && !HasIssue(issues, property.Name))
                {
                    issues.Add(new ValidationIssue(
                        property.Name,
                        ValidationCode.NotAllowed,
                        $"value {Convert.ToInt64(value, CultureInfo.InvariantCulture)} is not a valid {enumType.Name}"));
                }
            }
        }

        /// <summary>Проверяет конечность каждой точки импортированной ломаной.</summary>
        public static bool AddPolylineComplexityIssues(
            IList<ValidationIssue> issues,
            string collectionProperty,
            IReadOnlyList<Polyline2D> polylines)
        {
            if (polylines.Count > GenerationLimits.MaxImportedContoursPerOperation)
            {
                issues.Add(new ValidationIssue(
                    collectionProperty,
                    ValidationCode.AboveMaximum,
                    $"must contain at most {GenerationLimits.MaxImportedContoursPerOperation} contours, but contains {polylines.Count}",
                    GenerationLimits.MaxImportedContoursPerOperation));
                return false;
            }

            long pointCount = 0;
            foreach (var polyline in polylines)
                pointCount += polyline?.Points?.Count ?? 0;

            if (pointCount <= GenerationLimits.MaxImportedPointsPerOperation)
                return true;

            issues.Add(new ValidationIssue(
                collectionProperty,
                ValidationCode.AboveMaximum,
                $"must contain at most {GenerationLimits.MaxImportedPointsPerOperation} points, but contains {pointCount}",
                GenerationLimits.MaxImportedPointsPerOperation));
            return false;
        }

        public static void AddPolylinePointIssues(
            IList<ValidationIssue> issues,
            string collectionProperty,
            IReadOnlyList<Polyline2D> polylines)
        {
            for (var polylineIndex = 0; polylineIndex < polylines.Count; polylineIndex++)
            {
                var points = polylines[polylineIndex]?.Points;
                if (points == null)
                    continue;

                for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
                {
                    var point = points[pointIndex];
                    if (point == null)
                        continue;

                    AddIfNotFinite(issues,
                        $"{collectionProperty}[{polylineIndex}].Points[{pointIndex}].X", point.X);
                    AddIfNotFinite(issues,
                        $"{collectionProperty}[{polylineIndex}].Points[{pointIndex}].Y", point.Y);
                }
            }
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

        /// <summary>Вещественное значение не может быть ниже предела.</summary>
        public static void AddIfBelow(IList<ValidationIssue> issues, string property, double value, double min)
        {
            if (!double.IsFinite(value))
            {
                AddIfNotFinite(issues, property, value);
                return;
            }

            // Предел сам может быть невозможным числом: о нём сообщит
            // собственная проверка, и второе сообщение о том же — лишнее.
            if (!double.IsFinite(min))
                return;

            if (value < min)
                issues.Add(new ValidationIssue(property, ValidationCode.BelowMinimum,
                    $"must be at least {Text(min)}, but is {Text(value)}", min));
        }

        /// <summary>
        /// Значение должно быть строго выше предела: так проверяются высоты,
        /// на которых инструмент проходит над заготовкой.
        ///
        /// Равенство здесь — не «впритык допустимо», а перемещение вплотную
        /// к материалу: на быстрой подаче фреза пройдёт по самой поверхности,
        /// а любая неровность заготовки или биение станут ударом.
        /// </summary>
        /// <param name="issues">Список проблем, куда добавляются найденные.</param>
        /// <param name="property">Имя параметра.</param>
        /// <param name="value">Проверяемая высота.</param>
        /// <param name="limit">Уровень, выше которого она обязана быть.</param>
        public static void AddIfNotAbove(IList<ValidationIssue> issues, string property, double value, double limit)
        {
            if (!double.IsFinite(value))
            {
                AddIfNotFinite(issues, property, value);
                return;
            }

            if (!double.IsFinite(limit))
                return;

            if (value <= limit)
                issues.Add(new ValidationIssue(property, ValidationCode.NotAbove,
                    $"must be above {Text(limit)}, but is {Text(value)}", limit));
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
        /// Значение должно быть больше нуля и не больше предела.
        ///
        /// Так задаются подачи, обороты и задержки — всё, что уходит на
        /// станок числом и где лишний разряд опаснее самого значения.
        /// Проверка одна на оба конца намеренно: неположительное значение
        /// и значение выше предела — одна и та же ошибка ввода, и второе
        /// сообщение о том же поле пользователю ничего не добавляет.
        /// </summary>
        /// <param name="issues">Список проблем, куда добавляются найденные.</param>
        /// <param name="property">Имя параметра.</param>
        /// <param name="value">Проверяемое значение.</param>
        /// <param name="max">Наибольшее допустимое значение.</param>
        public static void AddIfOutOfPositiveRange(
            IList<ValidationIssue> issues, string property, double value, double max)
        {
            if (!double.IsFinite(value) || value <= 0)
            {
                AddIfNotPositive(issues, property, value);
                return;
            }

            if (value > max)
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
        /// Проверки, общие для любой операции резания: подачи, отвод и
        /// точность вывода координат.
        ///
        /// Нулевая рабочая подача — не мелочь: <c>F0</c> означает, что
        /// инструмент никуда не поедет, а стойка при этом либо остановится
        /// с ошибкой, либо будет ждать бесконечно. Раньше это правило жило
        /// отдельно для фрезеровки и отдельно для сверления, и добавленное
        /// в одном месте до другого не доходило: у сверления, например, так
        /// и не проверялись рабочая подача в плоскости и высота отвода.
        /// </summary>
        public static void AddCuttingIssues(IList<ValidationIssue> issues, CuttingOperationBase operation)
        {
            AddIfOutOfPositiveRange(issues, nameof(operation.FeedXYWork), operation.FeedXYWork, MaxWorkFeed);
            AddIfOutOfPositiveRange(issues, nameof(operation.FeedZWork), operation.FeedZWork, MaxWorkFeed);
            AddIfOutOfPositiveRange(issues, nameof(operation.FeedXYRapid), operation.FeedXYRapid, MaxRapidFeed);
            AddIfOutOfPositiveRange(issues, nameof(operation.FeedZRapid), operation.FeedZRapid, MaxRapidFeed);

            AddIfNegative(issues, nameof(operation.RetractHeight), operation.RetractHeight);

            AddIfOutOfRange(issues, nameof(operation.Decimals), operation.Decimals, 0, MaxDecimals);
            AddPublicValueIssues(issues, operation);
        }

        /// <summary>
        /// Полный набор проверок фрезерной операции: параметры резания,
        /// подачи, высоты и точность вывода.
        /// </summary>
        public static void AddMillingIssues(IList<ValidationIssue> issues, MillingOperationBase operation)
        {
            AddCommonMillingIssues(issues, operation.TotalDepth, operation.StepDepth, operation.ToolDiameter);
            AddCuttingIssues(issues, operation);

            EnumValidation.AddIfUndefined(issues, nameof(operation.Direction), operation.Direction);

            AddIfNotFinite(issues, nameof(operation.ContourHeight), operation.ContourHeight);
            AddIfNotFinite(issues, nameof(operation.SafeZHeight), operation.SafeZHeight);

            // Безопасная высота — та, на которой инструмент переносится над
            // заготовкой между контурами, слоями и областями. Прежде от неё
            // требовалось только быть числом, поэтому её можно было задать
            // ниже верха заготовки — например, оставить значение по умолчанию
            // при обработке выступа, где высота контура положительна. Тогда
            // каждый холостой переход шёл сквозь материал на быстрой подаче,
            // и ни программа, ни предпросмотр об этом не сообщали.
            AddIfNotAbove(
                issues, nameof(operation.SafeZHeight), operation.SafeZHeight, operation.ContourHeight);
        }

        /// <summary>
        /// Проверки, общие для карманов: подвод, шаг выборки и припуск.
        ///
        /// Шаг больше диаметра фрезы оставляет между проходами нетронутый
        /// материал — карман получится не выбранным, а расчерченным.
        /// </summary>
        public static void AddPocketIssues(IList<ValidationIssue> issues, PocketOperationBase operation)
        {
            // Остров не режется сам, но его геометрия используется другими
            // карманами и тоже обязана быть конечной.
            AddPublicValueIssues(issues, operation);
            EnumValidation.AddIfUndefined(issues, nameof(operation.PocketMode), operation.PocketMode);
            AddIfOutOfRange(
                issues,
                nameof(operation.WallTaperAngleDeg),
                operation.WallTaperAngleDeg,
                0,
                PocketOperationBase.MaxWallTaperAngleDeg);

            // Остров задаёт только запрещённую для резания геометрию. Подачи,
            // глубина, инструмент, стратегия и подвод у него не исполняются и
            // поэтому не должны мешать генерации других операций проекта.
            if (operation.PocketMode == PocketMode.Island)
                return;

            AddMillingIssues(issues, operation);

            EnumValidation.AddIfUndefined(issues, nameof(operation.EntryMode), operation.EntryMode);
            EnumValidation.AddIfUndefined(issues, nameof(operation.PocketStrategy), operation.PocketStrategy);
            EnumValidation.AddIfUndefined(
                issues, nameof(operation.ProcessingDirection), operation.ProcessingDirection);
            EnumValidation.AddIfUndefined(issues, nameof(operation.FinishingMode), operation.FinishingMode);

            AddIfOutOfRange(issues, nameof(operation.StepPercentOfTool), operation.StepPercentOfTool, 1, 100);
            AddIfNegative(issues, nameof(operation.FinishAllowance), operation.FinishAllowance);
            AddIfNotFinite(issues, nameof(operation.LineAngleDeg), operation.LineAngleDeg);

            // Угол и диаметр не влияют на вертикальный вход: старые проекты
            // могут не содержать этих полей и всё равно должны открываться.
            if (operation.EntryMode == PocketEntryMode.Helical)
            {
                AddIfOutOfRange(issues, nameof(operation.EntryAngle), operation.EntryAngle, 0.1, 89.9);
                AddIfNotPositive(issues, nameof(operation.HelicalEntryDiameter), operation.HelicalEntryDiameter);

                if (double.IsFinite(operation.EntryAngle)
                    && operation.EntryAngle >= 0.1
                    && operation.EntryAngle <= 89.9
                    && double.IsFinite(operation.HelicalEntryDiameter)
                    && operation.HelicalEntryDiameter > 0
                    && double.IsFinite(operation.TotalDepth)
                    && operation.TotalDepth > 0
                    && double.IsFinite(operation.StepDepth)
                    && operation.StepDepth > 0)
                {
                    // Винтовой вход начинается над верхом слоя на высоте
                    // отвода, поэтому длина спуска больше глубины самого слоя.
                    var layerDepth = Math.Min(operation.TotalDepth, operation.StepDepth)
                        + operation.RetractHeight;
                    var depthPerTurn = Math.PI * operation.HelicalEntryDiameter
                        * Math.Tan(operation.EntryAngle * Math.PI / 180.0);
                    var turns = layerDepth / depthPerTurn;
                    if (!double.IsFinite(turns) || turns > PocketOperationBase.MaxHelicalEntryTurnsPerLayer)
                    {
                        issues.Add(new ValidationIssue(
                            nameof(operation.EntryAngle),
                            ValidationCode.Inconsistent,
                            $"angle and diameter require {turns.ToString("0.###", CultureInfo.InvariantCulture)} "
                            + $"turns per layer; at most {PocketOperationBase.MaxHelicalEntryTurnsPerLayer} are allowed"));
                    }
                }
            }

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
        public static bool IsContourClosed(Polyline2D contour)
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
