#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Соответствие «способ выборки материала → стратегия».
    /// </summary>
    public interface IPocketStrategyRegistry
    {
        /// <summary>Все зарегистрированные способы — для проверки полноты.</summary>
        IReadOnlyDictionary<PocketStrategy, IPocketPocketingStrategy> All { get; }

        /// <summary>Стратегия для указанного способа выборки.</summary>
        IPocketPocketingStrategy For(PocketStrategy strategy);
    }

    /// <summary>
    /// Реестр стратегий выборки кармана — по образцу
    /// <see cref="OperationGeneratorRegistry"/>: экземпляр с интерфейсом
    /// вместо прежнего статического словаря. Статический словарь закрывал
    /// расширение снаружи: подключить стратегию из другого модуля или через
    /// контейнер было нельзя, а новая стратегия требовала правки самого
    /// словаря.
    ///
    /// Стратегия — чистое правило обхода: она читает слой и пишет
    /// перемещения, ничего не запоминая между вызовами, поэтому экземпляры
    /// создаются один раз на реестр.
    ///
    /// Неизвестное значение — отказ, а не подстановка спирали: файл проекта,
    /// принесший незнакомый способ, дал бы траекторию, не соответствующую
    /// тому, что в нём записано.
    /// </summary>
    public sealed class PocketStrategyRegistry : IPocketStrategyRegistry
    {
        private readonly Dictionary<PocketStrategy, IPocketPocketingStrategy> _strategies;

        /// <summary>Стандартный набор из пяти стратегий продукта.</summary>
        public PocketStrategyRegistry()
            : this(new Dictionary<PocketStrategy, IPocketPocketingStrategy>
            {
                [PocketStrategy.Spiral] = new SpiralPocketingStrategy(),
                [PocketStrategy.Concentric] = new ConcentricPocketingStrategy(),
                [PocketStrategy.Radial] = new RadialPocketingStrategy(),
                [PocketStrategy.ZigZag] = new ZigZagPocketingStrategy(),
                [PocketStrategy.Lines] = new LinesPocketingStrategy(),
            })
        {
        }

        /// <summary>Реестр из явного набора — для расширений и тестов.</summary>
        /// <param name="strategies">Соответствие «способ → стратегия».</param>
        public PocketStrategyRegistry(IReadOnlyDictionary<PocketStrategy, IPocketPocketingStrategy> strategies)
        {
            if (strategies == null)
                throw new ArgumentNullException(nameof(strategies));

            _strategies = new Dictionary<PocketStrategy, IPocketPocketingStrategy>();
            foreach (var entry in strategies)
                _strategies[entry.Key] = entry.Value;
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<PocketStrategy, IPocketPocketingStrategy> All => _strategies;

        /// <inheritdoc />
        public IPocketPocketingStrategy For(PocketStrategy strategy)
        {
            if (_strategies.TryGetValue(strategy, out var found))
                return found;

            throw new NotSupportedException(
                FormattableString.Invariant($"Pocket strategy {(int)strategy} is not registered."));
        }
    }
}
