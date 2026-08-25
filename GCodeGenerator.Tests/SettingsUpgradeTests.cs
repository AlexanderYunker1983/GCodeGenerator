using System.Collections.Generic;
using System.Configuration;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Перенос настроек между версиями программы.
    ///
    /// Файл настроек лежит в каталоге, имя которого содержит версию сборки,
    /// а версия берётся из git-тега — значит меняется с каждым релизом.
    /// Без переноса обновление показывало бы значения по умолчанию, а
    /// повторный перенос затирал бы то, что пользователь поменял уже
    /// в новой версии.
    /// </summary>
    [TestClass]
    public class SettingsUpgradeTests
    {
        [TestMethod]
        public void FirstRunOfNewVersion_UpgradesAndClearsTheFlag()
        {
            var persisted = new FakePersistedSettings { UpgradeRequired = true };

            _ = new AppSettingsStore(persisted);

            Assert.AreEqual(1, persisted.UpgradeCount, "Настройки предыдущей версии переносятся");
            Assert.IsFalse(persisted.UpgradeRequired, "Признак переноса сбрасывается");
            Assert.AreEqual(1, persisted.SaveCount, "Сброшенный признак сохраняется");
        }

        [TestMethod]
        public void NextRunOfSameVersion_DoesNotUpgrade()
        {
            var persisted = new FakePersistedSettings { UpgradeRequired = false };

            _ = new AppSettingsStore(persisted);

            Assert.AreEqual(0, persisted.UpgradeCount, "Повторный перенос затёр бы текущие настройки");
            Assert.AreEqual(0, persisted.SaveCount, "Без переноса запись не нужна");
        }

        /// <summary>
        /// Признак сбрасывается после переноса, а не до: <c>Upgrade</c>
        /// копирует значения предыдущей версии, включая сам признак, который
        /// там уже сброшен.
        /// </summary>
        [TestMethod]
        public void FlagIsClearedAfterUpgrade_NotBefore()
        {
            var persisted = new FakePersistedSettings { UpgradeRequired = true };
            persisted.OnUpgrade = () => persisted.UpgradeRequired = true;

            _ = new AppSettingsStore(persisted);

            Assert.IsFalse(persisted.UpgradeRequired,
                "Значение признака из предыдущей версии не должно пережить перенос");
        }

        /// <summary>
        /// Повреждённый или недоступный файл настроек не должен мешать запуску:
        /// программа стартует со значениями по умолчанию.
        /// </summary>
        [TestMethod]
        public void BrokenPreviousSettings_DoNotBreakStartup()
        {
            var persisted = new FakePersistedSettings { UpgradeRequired = true };
            persisted.OnUpgrade = () => throw new ConfigurationErrorsException("повреждённый user.config");

            var store = new AppSettingsStore(persisted);

            Assert.IsNotNull(store.Current, "Настройки создаются несмотря на сбой переноса");
        }

        private sealed class FakePersistedSettings : IPersistedSettings
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public System.Action OnUpgrade { get; set; }

            public int UpgradeCount { get; private set; }

            public int SaveCount { get; private set; }

            public bool UpgradeRequired { get; set; }

            public object this[string name]
            {
                get => _values.TryGetValue(name, out var value) ? value : DefaultFor(name);
                set => _values[name] = value;
            }

            public void Upgrade()
            {
                UpgradeCount++;
                OnUpgrade?.Invoke();
            }

            public void Save() => SaveCount++;

            /// <summary>
            /// Значения по умолчанию нужного типа: хранилище читается по всей
            /// таблице маппинга, а тест задаёт только признак переноса.
            /// </summary>
            private static object DefaultFor(string name)
            {
                var property = typeof(GCodeGenerator.Properties.Settings)
                    .GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(property, $"Настройка {name} должна быть объявлена в Properties.Settings");

                if (property.PropertyType == typeof(string))
                    return string.Empty;
                return System.Activator.CreateInstance(property.PropertyType);
            }
        }
    }
}
