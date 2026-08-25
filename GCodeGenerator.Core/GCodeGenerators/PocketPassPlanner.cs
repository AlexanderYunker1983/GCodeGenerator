#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.GCodeGenerators.Interfaces;
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
        public PocketPass(IPocketOperation operation, PocketPassKind kind, double allowance = 0.0)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Kind = kind;
            Allowance = allowance;
        }

        /// <summary>Операция прохода: копия исходной с изменённой глубиной.</summary>
        public IPocketOperation Operation { get; }

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

        public static PocketPassPlan Plan(IPocketOperation operation)
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
        private static PocketPass? PlanRoughing(IPocketOperation operation, double allowance, out string? skipComment)
        {
            skipComment = null;

            var depthAllowance = Math.Min(allowance, Math.Max(0.0, operation.TotalDepth - NegligibleDepth));
            if (depthAllowance <= 0)
                return new PocketPass(operation, PocketPassKind.Pocketing);

            var roughOperation = OperationCloner.Clone(operation);
            roughOperation.TotalDepth -= depthAllowance;

            if (IsTooSmall(roughOperation, depthAllowance))
            {
                skipComment = ProgramComments.PocketTooSmallForAllowance;
                return null;
            }

            return new PocketPass(roughOperation, PocketPassKind.Pocketing, depthAllowance);
        }

        /// <summary>
        /// Чистовые проходы работают только в слое припуска: дно снимается
        /// выборкой «толстым» инструментом, стенка — обходом по контуру.
        /// </summary>
        private static void AddFinishingPasses(IPocketOperation operation, double allowance, List<PocketPass> passes)
        {
            var depthAllowance = Math.Min(allowance, Math.Max(0.0, operation.TotalDepth));
            if (depthAllowance < NegligibleDepth)
                return;

            var layer = OperationCloner.Clone(operation);
            layer.ContourHeight = operation.ContourHeight - (operation.TotalDepth - depthAllowance);
            layer.TotalDepth = depthAllowance;
            layer.IsRoughingEnabled = false;
            layer.IsFinishingEnabled = false;
            layer.FinishAllowance = allowance;

            var finishesBottom = operation.FinishingMode != PocketFinishingMode.Walls;
            var finishesWalls = operation.FinishingMode != PocketFinishingMode.Bottom;

            if (finishesBottom)
            {
                // Дно слоя припуска снимается с тем же отступом от стенки:
                // саму стенку доводит отдельный проход.
                if (!IsTooSmall(layer, allowance))
                    passes.Add(new PocketPass(OperationCloner.Clone(layer), PocketPassKind.Pocketing, allowance));
            }

            if (finishesWalls)
                passes.Add(new PocketPass(layer, PocketPassKind.WallFinishing));
        }

        /// <summary>
        /// Не исчез ли карман после припуска: контур проверяется в худшем
        /// месте — на дне, где уклон стенки съедает больше всего.
        /// </summary>
        private static bool IsTooSmall(IPocketOperation operation, double allowance)
        {
            var contourOffset = operation.ToolDiameter / 2.0 + allowance;
            var taperOffset = GCodeGenerationHelper.CalculateTaperOffset(operation.TotalDepth, operation.WallTaperAngleDeg);
            return OperationCatalog.CreatePocketGeometry((OperationBase)operation).IsContourTooSmall(contourOffset, taperOffset);
        }
    }
}
