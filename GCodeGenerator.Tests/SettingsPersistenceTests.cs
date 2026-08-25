using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class SettingsPersistenceTests
    {
        /// <summary>
        /// Служебные настройки, не описывающие генерацию и потому отсутствующие
        /// в <see cref="GCodeSettings"/>: признак переноса настроек из файла
        /// предыдущей версии программы.
        /// </summary>
        private static readonly HashSet<string> ServiceSettings = new HashSet<string>
        {
            "UpgradeRequired"
        };

        /// <summary>
        /// Новое leaf-свойство в GCodeSettings нельзя незаметно забыть при
        /// загрузке/сохранении Properties.Settings: обе стороны используют
        /// SettingsMapping, а этот тест проверяет полноту и взаимно-однозначность.
        /// </summary>
        [TestMethod]
        public void SettingsMapping_CoversEveryLeafAndPersistentSettingExactlyOnce()
        {
            var settings = new GCodeSettings();
            var expectedPaths = typeof(GCodeSettings)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(group => group.PropertyType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(leaf => leaf.CanRead && leaf.CanWrite)
                    .Select(leaf => $"{group.Name}.{leaf.Name}"))
                .ToArray();

            var mappedPaths = SettingsMapping.Entries.Select(entry => entry.Path).ToArray();
            var mappedSettings = SettingsMapping.Entries.Select(entry => entry.Setting).ToArray();
            var persistentSettings = GCodeGenerator.Properties.Settings.Default.Properties
                .Cast<SettingsProperty>()
                .Select(property => property.Name)
                .Where(name => !ServiceSettings.Contains(name))
                .ToArray();

            CollectionAssert.AreEquivalent(expectedPaths, mappedPaths,
                "SettingsMapping должен содержать каждое leaf-свойство GCodeSettings");
            CollectionAssert.AreEquivalent(persistentSettings, mappedSettings,
                "SettingsMapping должен содержать каждую запись Properties.Settings");
            Assert.AreEqual(mappedPaths.Length, new HashSet<string>(mappedPaths).Count,
                "Пути модели в SettingsMapping не должны повторяться");
            Assert.AreEqual(mappedSettings.Length, new HashSet<string>(mappedSettings).Count,
                "Имена Properties.Settings в SettingsMapping не должны повторяться");

            foreach (var path in mappedPaths)
                Assert.IsNotNull(SettingsMapping.GetValue(settings, path), $"Путь {path} должен читаться");
        }
    }
}
