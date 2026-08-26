using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Реестр постпроцессоров: стойка выбирается настройкой
    /// Format.PostProcessorName, и единицы аргумента паузы у стоек разные,
    /// поэтому подмена стойки — это программа, которая исполнится неверно.
    /// </summary>
    [TestClass]
    public class PostProcessorRegistryTests
    {
        private static readonly PostProcessorRegistry Registry = new PostProcessorRegistry();

        /// <summary>
        /// Настройки по умолчанию находят свою стойку: строка «Generic»
        /// в GCodeFormatSettings написана отдельно от ключа постпроцессора
        /// (слой моделей не зависит от генераторов), и разойтись им нельзя —
        /// новый проект перестал бы генерироваться вовсе.
        /// </summary>
        [TestMethod]
        public void DefaultSettings_FindTheirPostProcessor()
        {
            var byDefaultName = Registry.Find(new GCodeFormatSettings().PostProcessorName);

            Assert.IsNotNull(byDefaultName);
            Assert.IsInstanceOfType(byDefaultName, typeof(GenericPostProcessor));
            Assert.AreEqual("Generic", byDefaultName.Key);
        }

        /// <summary>Стандартный набор: Generic и GRBL, ключи различны.</summary>
        [TestMethod]
        public void DefaultRegistry_ContainsGenericAndGrbl()
        {
            var keys = Registry.All.Select(p => p.Key).ToList();

            CollectionAssert.AreEquivalent(new List<string> { "Generic", "GRBL" }, keys);
        }

        /// <summary>
        /// Ключ приходит из настроек и файла проекта, которые пользователь
        /// может редактировать руками: «grbl» вместо «GRBL» — не повод
        /// объявить проект негодным.
        /// </summary>
        [TestMethod]
        public void Find_IgnoresCase()
        {
            Assert.IsInstanceOfType(Registry.Find("grbl"), typeof(GrblPostProcessor));
            Assert.IsInstanceOfType(Registry.Find("GENERIC"), typeof(GenericPostProcessor));
        }

        /// <summary>
        /// Неизвестный ключ — отказ с перечислением допустимых, а не
        /// подстановка Generic: программа, молча построенная не для той
        /// стойки, исполнялась бы неверно.
        /// </summary>
        [TestMethod]
        public void UnknownKey_IsRefusedWithKnownKeys()
        {
            Assert.IsNull(Registry.Find("Mazak"));

            var failure = Assert.Throws<NotSupportedException>(() => Registry.For("Mazak"));

            StringAssert.Contains(failure.Message, "Mazak");
            StringAssert.Contains(failure.Message, "Generic");
            StringAssert.Contains(failure.Message, "GRBL");
        }

        /// <summary>
        /// Два постпроцессора с одним ключом делают выбор в настройках
        /// неоднозначным: какой строил бы программу — зависело бы от порядка
        /// регистрации.
        /// </summary>
        [TestMethod]
        public void DuplicateKeys_AreRefused()
        {
            var failure = Assert.Throws<ArgumentException>(() => new PostProcessorRegistry(
                new IPostProcessor[] { new GenericPostProcessor(), new GenericPostProcessor() }));

            StringAssert.Contains(failure.Message, "Generic");
        }

        /// <summary>
        /// Главное отличие GRBL: аргумент паузы G4 — секунды. Программа
        /// с P2500 на GRBL простояла бы сорок минут вместо двух с половиной
        /// секунд.
        /// </summary>
        [TestMethod]
        public void Grbl_WritesDwellInSeconds()
        {
            var generator = new SimpleGCodeGenerator();
            var operations = Ops(OperationFixtures.DrillPoints());

            var generic = generator.Generate(operations, SettingsFixtures.SpindleDelay());
            var grbl = generator.Generate(operations, SettingsFixtures.GrblSpindleDelay());

            Assert.IsTrue(generic.Lines.Any(l => l.Contains("G4 P2500")), "Generic: миллисекунды");
            Assert.IsTrue(grbl.Lines.Any(l => l.Contains("G4 P2.5")), "GRBL: секунды");
        }

        /// <summary>
        /// Кроме паузы стойки не отличаются ничем: одинаковый состав
        /// программы — это гарантия, что выбор GRBL не перестраивает
        /// траекторию, а меняет ровно единицу аргумента G4.
        /// </summary>
        [TestMethod]
        public void Grbl_DiffersFromGeneric_OnlyInDwell()
        {
            var generator = new SimpleGCodeGenerator();
            var operations = Ops(OperationFixtures.DrillPoints());

            var generic = generator.Generate(operations, SettingsFixtures.SpindleDelay()).Lines;
            var grbl = generator.Generate(operations, SettingsFixtures.GrblSpindleDelay()).Lines;

            Assert.AreEqual(generic.Count, grbl.Count, "Число строк совпадает");

            var differences = Enumerable.Range(0, generic.Count)
                .Where(i => generic[i] != grbl[i])
                .ToList();

            Assert.AreEqual(1, differences.Count, "Отличие ровно одно");
            StringAssert.Contains(generic[differences[0]], "G4", "И это строка паузы");
        }

        private static List<OperationBase> Ops(OperationBase operation)
            => new List<OperationBase> { operation };
    }
}
