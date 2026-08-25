#nullable enable
using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Strategies
{
    /// <summary>
    /// Соответствие «способ выборки материала → стратегия».
    ///
    /// Стратегия — чистое правило обхода: она читает слой и пишет
    /// перемещения, ничего не запоминая между вызовами. Поэтому создавать её
    /// заново на каждый слой не нужно, и раньше это делалось лишь потому,
    /// что выбор стратегии был написан переключателем прямо в генераторе.
    ///
    /// Неизвестное значение — отказ, а не подстановка спирали: файл проекта,
    /// принесший незнакомый способ, дал бы траекторию, не соответствующую
    /// тому, что в нём записано.
    /// </summary>
    public static class PocketStrategies
    {
        private static readonly Dictionary<PocketStrategy, IPocketPocketingStrategy> Registry =
            new Dictionary<PocketStrategy, IPocketPocketingStrategy>
            {
                [PocketStrategy.Spiral] = new SpiralPocketingStrategy(),
                [PocketStrategy.Concentric] = new ConcentricPocketingStrategy(),
                [PocketStrategy.Radial] = new RadialPocketingStrategy(),
                [PocketStrategy.ZigZag] = new ZigZagPocketingStrategy(),
                [PocketStrategy.Lines] = new LinesPocketingStrategy(),
            };

        /// <summary>Все зарегистрированные способы — для проверки полноты.</summary>
        public static IReadOnlyDictionary<PocketStrategy, IPocketPocketingStrategy> All => Registry;

        /// <summary>Стратегия для указанного способа выборки.</summary>
        public static IPocketPocketingStrategy For(PocketStrategy strategy)
        {
            if (Registry.TryGetValue(strategy, out var found))
                return found;

            throw new NotSupportedException(
                $"Стратегия обработки кармана {(int)strategy} не поддерживается.");
        }
    }
}
