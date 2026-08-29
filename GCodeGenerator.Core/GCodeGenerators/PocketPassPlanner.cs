#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>Чем занимается проход обработки кармана.</summary>
    public enum PocketPassKind
    {
        /// <summary>Выборка материала выбранной стратегией.</summary>
        Pocketing,

        /// <summary>Чистовой проход по стенке замкнутым контуром.</summary>
        WallFinishing
    }

    /// <summary>Один проход обработки: что фрезеровать и каким способом.</summary>
    public sealed class PocketPass
    {
        public PocketPass(PocketOperationBase operation, PocketPassKind kind, double allowance = 0.0)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Kind = kind;
            Allowance = allowance;
        }

        /// <summary>Операция прохода: копия исходной с изменённой глубиной.</summary>
        public PocketOperationBase Operation { get; }

        /// <summary>Способ обработки.</summary>
        public PocketPassKind Kind { get; }

        /// <summary>
        /// Припуск у стенки: на столько траектория этого прохода отступает
        /// внутрь от контура кармана. Диаметр инструмента при этом остаётся
        /// настоящим — от него считается шаг между проходами.
        /// </summary>
        public double Allowance { get; }
    }

    /// <summary>
    /// План обработки кармана: проходы по порядку и общая точка отсчёта уклона.
    /// </summary>
    public sealed class PocketPassPlan
    {
        public PocketPassPlan(IReadOnlyList<PocketPass> passes, double taperOriginZ, string? skipComment = null)
        {
            Passes = passes ?? Array.Empty<PocketPass>();
            TaperOriginZ = taperOriginZ;
            SkipComment = skipComment;
        }

        /// <summary>Проходы в порядке выполнения.</summary>
        public IReadOnlyList<PocketPass> Passes { get; }

        /// <summary>
        /// Z, от которой отсчитывается уклон стенки. У чистовых проходов
        /// обрабатывается только слой припуска, но уклон должен продолжать
        /// стенку исходного кармана, а не начинаться заново от верха слоя.
        /// </summary>
        public double TaperOriginZ { get; }

        /// <summary>
        /// Причина, по которой обработка не выполняется, или <c>null</c>.
        /// Попадает в программу комментарием.
        /// </summary>
        public string? SkipComment { get; }
    }

    /// <summary>
    /// Раскладывает карман на проходы: черновой с припуском, чистовой по дну
    /// и чистовой по стенке.
    ///
    /// Прежде эта логика жила в методе с семью делегатами — клонированием,
    /// применением припуска, проверкой размера и двумя способами генерации:
    /// последовательность проходов приходилось восстанавливать по цепочке
    /// вызовов. Теперь план строится отдельно и проверяется без генерации
    /// G-code, а генератор только исполняет готовый список.
    ///
    /// Припуск проход несёт отдельной величиной и отступает на неё от
    /// стенки. Прежде он подмешивался в диаметр инструмента: контур от этого
    /// получался правильный, но шаг между проходами считался от несуществующей
    /// фрезы, которая шире настоящей, — и между проходами оставался
    /// нетронутый материал тем шире, чем больше припуск.
    /// </summary>
    public static class PocketPassPlanner
    {
        /// <summary>Глубина ниже этого значения считается нулевой, мм.</summary>
        private const double NegligibleDepth = 1e-6;

        public static PocketPassPlan Plan(PocketOperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            var passes = new List<PocketPass>();
            var taperOriginZ = operation.ContourHeight;

            var roughing = operation.IsRoughingEnabled;
            var finishing = operation.IsFinishingEnabled;
            var allowance = Math.Max(0.0, operation.FinishAllowance);

            // Оба режима выключены — обычная выборка без припуска.
            if (!roughing && !finishing)
            {
                roughing = true;
                allowance = 0.0;
            }

            if (roughing)
            {
                var roughingPass = PlanRoughing(operation, allowance, out var skipComment);

                // Причина отказа и проход исключают друг друга: карман либо
                // исчез под припуском, либо обрабатывается.
                if (roughingPass == null)
                    return new PocketPassPlan(Array.Empty<PocketPass>(), taperOriginZ, skipComment);

                passes.Add(roughingPass);
            }

            if (finishing && allowance > 0)
                AddFinishingPasses(operation, allowance, passes);

            return new PocketPassPlan(passes, taperOriginZ);
        }

        /// <summary>
        /// Черновой проход: не доходит до дна на величину припуска и идёт
        /// «толстым» инструментом, оставляя припуск и на стенке.
        /// </summary>
        private static PocketPass? PlanRoughing(PocketOperationBase operation, double allowance, out string? skipComment)
        {
            skipComment = null;

            var bottomAllowance = Math.Min(allowance, Math.Max(0.0, operation.TotalDepth - NegligibleDepth));
            if (bottomAllowance <= 0)
                return new PocketPass(operation, PocketPassKind.Pocketing);

            var roughOperation = OperationCloner.Clone(operation);
            roughOperation.TotalDepth -= bottomAllowance;

            // Припуск дна ограничен общей глубиной, но припуск стенки — нет:
            // это независимое расстояние в плоскости XY. Прежде мелкий
            // карман глубиной 0,5 мм обрезал заданный стеновой припуск 1 мм
            // до 0,5 мм и оставлял чистовой проход без ожидаемого материала.
            if (IsTooSmall(roughOperation, allowance))
            {
                skipComment = ProgramComments.PocketTooSmallForAllowance;
                return null;
            }

            return new PocketPass(roughOperation, PocketPassKind.Pocketing, allowance);
        }

        /// <summary>
        /// Чистовые проходы. Дно снимается выборкой в слое припуска у дна:
        /// выше него материала на дне нет. Стенка — другое дело: черновой
        /// проход отступает от неё на припуск в каждом слое, поэтому припуск
        /// лежит на всей высоте стенки, и чистовой обход контура выполняется
        /// по всем слоям до полной глубины. Прежде стенка доводилась только
        /// в слое припуска у дна — выше карман оставался уже задуманного
        /// на величину припуска.
        /// </summary>
        private static void AddFinishingPasses(PocketOperationBase operation, double allowance, List<PocketPass> passes)
        {
            var depthAllowance = Math.Min(allowance, Math.Max(0.0, operation.TotalDepth));
            if (depthAllowance < NegligibleDepth)
                return;

            var finishesBottom = operation.FinishingMode != PocketFinishingMode.Walls;
            var finishesWalls = operation.FinishingMode != PocketFinishingMode.Bottom;

            if (finishesBottom)
            {
                var bottom = OperationCloner.Clone(operation);
                bottom.ContourHeight = operation.ContourHeight - (operation.TotalDepth - depthAllowance);
                bottom.TotalDepth = depthAllowance;
                bottom.IsRoughingEnabled = false;
                bottom.IsFinishingEnabled = false;
                bottom.FinishAllowance = allowance;

                // Дно слоя припуска снимается с тем же отступом от стенки:
                // саму стенку доводит отдельный проход.
                if (!IsTooSmall(bottom, allowance))
                    passes.Add(new PocketPass(bottom, PocketPassKind.Pocketing, allowance));
            }

            if (finishesWalls)
            {
                var walls = OperationCloner.Clone(operation);
                walls.IsRoughingEnabled = false;
                walls.IsFinishingEnabled = false;
                walls.FinishAllowance = allowance;
                passes.Add(new PocketPass(walls, PocketPassKind.WallFinishing));
            }
        }

        /// <summary>
        /// Не исчез ли карман после припуска: контур проверяется в худшем
        /// месте — на дне, где уклон стенки съедает больше всего.
        /// </summary>
        private static bool IsTooSmall(PocketOperationBase operation, double allowance)
        {
            var contourOffset = operation.ToolDiameter / 2.0 + allowance;
            var taperOffset = GCodeGenerationHelper.CalculateTaperOffset(operation.TotalDepth, operation.WallTaperAngleDeg);
            return OperationCatalog.CreatePocketGeometry(operation).IsContourTooSmall(contourOffset, taperOffset);
        }
    }
}
