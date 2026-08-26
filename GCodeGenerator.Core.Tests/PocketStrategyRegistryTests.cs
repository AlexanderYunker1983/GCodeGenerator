using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Strategies;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Реестр способов выборки материала из кармана.
    ///
    /// Способ выбирается пользователем и сохраняется в проекте, поэтому
    /// пропуск в реестре означает либо отказ сгенерировать программу, либо —
    /// что хуже — обработку не тем способом, который записан в проекте.
    /// </summary>
    [TestClass]
    public class PocketStrategyRegistryTests
    {
        private static readonly PocketStrategyRegistry Registry = new PocketStrategyRegistry();

        /// <summary>
        /// Каждый способ из перечисления умеет обрабатывать слой: новый
        /// способ, добавленный в перечисление и в окно, но забытый здесь,
        /// доходил бы до генерации и падал на ней.
        /// </summary>
        [TestMethod]
        public void EveryStrategyOfEnum_IsRegistered()
        {
            foreach (PocketStrategy strategy in Enum.GetValues(typeof(PocketStrategy)))
            {
                Assert.IsNotNull(Registry.For(strategy), strategy.ToString());
            }

            Assert.AreEqual(
                Enum.GetValues(typeof(PocketStrategy)).Length,
                Registry.All.Count,
                "В реестре не должно быть записей без значения перечисления");
        }

        /// <summary>
        /// Способы различны между собой: общая реализация на два значения
        /// означала бы, что выбор пользователя ни на что не влияет.
        /// </summary>
        [TestMethod]
        public void EveryStrategy_IsDistinct()
        {
            var byInstance = new Dictionary<IPocketPocketingStrategy, PocketStrategy>();

            foreach (PocketStrategy strategy in Enum.GetValues(typeof(PocketStrategy)))
            {
                var instance = Registry.For(strategy);

                Assert.IsFalse(byInstance.ContainsKey(instance),
                    $"{strategy}: та же реализация, что и у {(byInstance.TryGetValue(instance, out var other) ? other : strategy)}");
                byInstance[instance] = strategy;
            }
        }

        /// <summary>
        /// Стратегия не помнит ничего между слоями, поэтому существует в
        /// одном экземпляре: прежде она создавалась заново на каждый слой.
        /// </summary>
        [TestMethod]
        public void Strategy_IsTheSameInstanceOnEveryRequest()
        {
            foreach (PocketStrategy strategy in Enum.GetValues(typeof(PocketStrategy)))
            {
                Assert.AreSame(Registry.For(strategy), Registry.For(strategy), strategy.ToString());
            }
        }

        /// <summary>
        /// Реестр без единой стратегии отвергается при создании: он не
        /// построил бы ни один карман, а отказ приходил бы на каждой
        /// генерации вместо одного раза при ошибочной конфигурации.
        /// </summary>
        [TestMethod]
        public void EmptySet_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new PocketStrategyRegistry(
                new Dictionary<PocketStrategy, IPocketPocketingStrategy>()));
        }

        /// <summary>
        /// Значение вне перечисления — отказ с указанием значения: файл
        /// проекта, принесший незнакомый способ, не должен молча обрабатываться
        /// каким-то другим.
        /// </summary>
        [TestMethod]
        public void UnknownStrategy_IsRefusedWithItsValue()
        {
            var unknown = (PocketStrategy)Enum.GetValues(typeof(PocketStrategy)).Cast<int>().Max() + 1;

            var failure = Assert.Throws<NotSupportedException>(() => Registry.For(unknown));

            StringAssert.Contains(failure.Message, ((int)unknown).ToString());
        }
    }
}
