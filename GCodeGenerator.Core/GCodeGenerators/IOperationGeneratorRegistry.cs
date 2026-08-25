#nullable enable
using System;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Maps operation types to their <see cref="IOperationGenerator"/>
    /// (plan item 4.5). Replaces the name-based reflection in
    /// <c>SimpleGCodeGenerator.LoadGenerators</c>: the mapping is explicit
    /// and resolvable through IoC.
    /// </summary>
    public interface IOperationGeneratorRegistry
    {
        /// <summary>
        /// Returns the generator for the exact operation type.
        /// </summary>
        /// <param name="operationType">Exact runtime type of the operation.</param>
        /// <param name="generator">Resolved generator, or null when not registered.</param>
        /// <returns>True when the operation type has a registered generator.</returns>
        bool TryGetGenerator(Type operationType, out IOperationGenerator? generator);
    }
}
