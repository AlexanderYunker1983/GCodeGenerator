using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Соответствие типа операции и генератора G-кода.
    ///
    /// Карта строится по <see cref="OperationCatalog"/>: генератор выбирается
    /// категорией операции — сверление, профиль или карман. Раньше типы
    /// перечислялись здесь заново, и новый тип операции, забытый в этом
    /// списке, молча пропускался при генерации.
    /// </summary>
    public sealed class OperationGeneratorRegistry : IOperationGeneratorRegistry
    {
        private readonly Dictionary<Type, IOperationGenerator> _generators;

        /// <summary>
        /// Стандартный набор генераторов: сверление —
        /// <see cref="DrillPointsOperationGenerator"/>, профили —
        /// <see cref="UnifiedProfileGenerator"/>, карманы —
        /// <see cref="UnifiedPocketGenerator"/>.
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
            _generators = new Dictionary<Type, IOperationGenerator>();
            foreach (var descriptor in OperationCatalog.All)
            {
                _generators[descriptor.OperationType] = descriptor.Category switch
                {
                    OperationCategory.Drill => drillPointsGenerator,
                    OperationCategory.Profile => profileGenerator,
                    OperationCategory.Pocket => pocketGenerator,
                    _ => throw new NotSupportedException(
                        $"Для категории {descriptor.Category} не задан генератор G-кода."),
                };
            }
        }

        public bool TryGetGenerator(Type operationType, out IOperationGenerator generator)
        {
            return _generators.TryGetValue(operationType, out generator);
        }
    }
}
