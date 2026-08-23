using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Explicit operation type → generator map (plan item 4.5).
    /// Replaces the name-based reflection in <c>SimpleGCodeGenerator.LoadGenerators</c>:
    /// every operation type is listed here, so a new operation type that is
    /// forgotten in this map fails the coverage test instead of being
    /// silently skipped during generation.
    /// </summary>
    public sealed class OperationGeneratorRegistry : IOperationGeneratorRegistry
    {
        private readonly Dictionary<Type, IOperationGenerator> _generators;

        /// <summary>
        /// Default constructor with the standard explicit mapping:
        /// drill → <see cref="DrillPointsOperationGenerator"/>,
        /// all profile operations → <see cref="UnifiedProfileGenerator"/>,
        /// all pocket operations → <see cref="UnifiedPocketGenerator"/>.
        /// </summary>
        public OperationGeneratorRegistry()
            : this(
                new DrillPointsOperationGenerator(),
                new UnifiedProfileGenerator(),
                new UnifiedPocketGenerator())
        {
        }

        public OperationGeneratorRegistry(
            IOperationGenerator drillPointsGenerator,
            IOperationGenerator profileGenerator,
            IOperationGenerator pocketGenerator)
        {
            _generators = new Dictionary<Type, IOperationGenerator>
            {
                // Сверление (9 режимов — один тип операции)
                [typeof(DrillPointsOperation)] = drillPointsGenerator,

                // Профили (6 типов)
                [typeof(ProfileCircleOperation)] = profileGenerator,
                [typeof(ProfileEllipseOperation)] = profileGenerator,
                [typeof(ProfilePolygonOperation)] = profileGenerator,
                [typeof(ProfileRectangleOperation)] = profileGenerator,
                [typeof(ProfileRoundedRectangleOperation)] = profileGenerator,
                [typeof(ProfileDxfOperation)] = profileGenerator,

                // Карманы (4 типа)
                [typeof(PocketCircleOperation)] = pocketGenerator,
                [typeof(PocketEllipseOperation)] = pocketGenerator,
                [typeof(PocketRectangleOperation)] = pocketGenerator,
                [typeof(PocketDxfOperation)] = pocketGenerator,
            };
        }

        public bool TryGetGenerator(Type operationType, out IOperationGenerator generator)
        {
            return _generators.TryGetValue(operationType, out generator);
        }
    }
}
