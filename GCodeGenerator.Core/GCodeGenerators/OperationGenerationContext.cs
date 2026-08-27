#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Сведения обо всём документе, которые нужны генератору одной операции.
    /// Большинство операций независимы; карманам контекст передаёт включённые
    /// острова, потому что их геометрия влияет на соседние операции.
    /// </summary>
    public sealed class OperationGenerationContext
    {
        public static readonly OperationGenerationContext Empty =
            new OperationGenerationContext(Array.Empty<PocketOperationBase>());

        public OperationGenerationContext(IReadOnlyList<PocketOperationBase> pocketIslands)
        {
            PocketIslands = pocketIslands ?? throw new ArgumentNullException(nameof(pocketIslands));
        }

        public IReadOnlyList<PocketOperationBase> PocketIslands { get; }

        public static OperationGenerationContext FromOperations(IReadOnlyList<OperationBase?> operations)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));

            return new OperationGenerationContext(
                operations
                    .OfType<PocketOperationBase>()
                    .Where(operation => operation.IsEnabled && operation.PocketMode == PocketMode.Island)
                    .ToList());
        }
    }

    /// <summary>
    /// Дополнительный контракт для генераторов, которым недостаточно одной
    /// операции. Базовый интерфейс остаётся прежним, поэтому независимые
    /// генераторы и расширения не обязаны принимать контекст документа.
    /// </summary>
    public interface IContextualOperationGenerator : IOperationGenerator
    {
        void Generate(
            OperationBase operation,
            Toolpath.ToolPathBuilder builder,
            GCodeSettings settings,
            OperationGenerationContext context,
            System.Threading.CancellationToken cancellation = default);
    }
}
