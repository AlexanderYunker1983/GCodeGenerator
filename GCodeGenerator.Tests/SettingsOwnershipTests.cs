using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Принадлежность настроек: генерационные секции — документу, Ui —
    /// приложению, умолчания новых проектов — постоянному хранилищу.
    ///
    /// Прежде OK окна настроек записывал в user.config все секции разом,
    /// поэтому настройки открытого проекта затирали умолчания приложения,
    /// а смена темы помечала проект несохранённым и сбрасывала программу.
    /// </summary>
    [TestClass]
    public class SettingsOwnershipTests
    {
        /// <summary>
        /// Смена темы — дело приложения: она сохраняется, но не трогает
        /// ни программу, ни признак несохранённого проекта.
        /// </summary>
        [TestMethod]
        public void UiOnlyChange_IsSavedWithoutGenerationEvent()
        {
            var persisted = new InMemoryPersistedSettings();
            var store = new AppSettingsStore(persisted);
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;

            store.Current.Ui.UseDarkTheme = true;
            store.Save();

            Assert.AreEqual(0, raised, "Тема не влияет ни на программу, ни на файл проекта");
            Assert.AreEqual(true, persisted["UseDarkTheme"], "Ui-настройка записана");
        }

        /// <summary>
        /// Генерационные настройки после OK принадлежат документу: событие
        /// поднимается, но умолчания приложения в хранилище не меняются.
        /// </summary>
        [TestMethod]
        public void GenerationChange_RaisesEventButDoesNotLeakIntoDefaults()
        {
            var persisted = new InMemoryPersistedSettings();
            var store = new AppSettingsStore(persisted);
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;

            store.Current.Format.LineNumberStep = 77;
            store.Save();

            Assert.AreEqual(1, raised, "Программа устарела, проект несохранён");
            Assert.AreNotEqual(77, persisted["LineNumberStep"],
                "Настройка документа не должна становиться умолчанием приложения");
        }

        /// <summary>
        /// Событие — по фактической разнице: OK без правок ничего не сообщает,
        /// и открытое окно настроек само по себе не пачкает проект.
        /// </summary>
        [TestMethod]
        public void SaveWithoutChanges_RaisesNothing()
        {
            var store = new AppSettingsStore(new InMemoryPersistedSettings());
            store.Current.Format.LineNumberStep = 77;
            store.Save();
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;

            store.Save();

            Assert.AreEqual(0, raised, "Ничего не изменилось — сообщать не о чем");
        }

        /// <summary>
        /// «Сделать умолчаниями» пишет генерационные секции источника в
        /// хранилище, не трогая ни Ui, ни настройки открытого документа.
        /// </summary>
        [TestMethod]
        public void SaveGenerationDefaults_WritesGenerationSectionsOnly()
        {
            var persisted = new InMemoryPersistedSettings();
            var store = new AppSettingsStore(persisted);
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;
            var documentStep = store.Current.Format.LineNumberStep;

            var defaults = new GCodeSettings();
            defaults.Format.LineNumberStep = 77;
            defaults.Ui.UseDarkTheme = true;
            store.SaveGenerationDefaults(defaults);

            Assert.AreEqual(77, persisted["LineNumberStep"], "Умолчание записано");
            Assert.AreNotEqual(true, persisted["UseDarkTheme"], "Ui-настройки не задаются этой командой");
            Assert.AreEqual(documentStep, store.Current.Format.LineNumberStep, "Документ не изменился");
            Assert.AreEqual(0, raised, "Настройки документа не менялись — события нет");
        }

        /// <summary>
        /// Открытие проекта: пришедшие секции применяются, отсутствующие
        /// возвращаются к умолчаниям приложения, а не наследуются от
        /// предыдущего открытого проекта.
        /// </summary>
        [TestMethod]
        public void ApplyProjectSettings_RestoresDefaultsForMissingSections()
        {
            var store = new AppSettingsStore(new InMemoryPersistedSettings());
            var defaultRpm = store.Current.Spindle.SpindleSpeedRpm;
            store.Current.Spindle.SpindleSpeedRpm = 9999;
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;

            var format = new GCodeFormatSettings { LineNumberStep = 5 };
            store.ApplyProjectSettings(format, null, null, null);

            Assert.AreEqual(5, store.Current.Format.LineNumberStep, "Секция файла применена");
            Assert.AreEqual(defaultRpm, store.Current.Spindle.SpindleSpeedRpm,
                "Отсутствующая секция — умолчание приложения, а не наследство прежнего проекта");
            Assert.AreEqual(1, raised, "Настройки документа сменились — программа устарела");
        }

        /// <summary>
        /// Разница считается по содержимому, а не по экземплярам: повторное
        /// открытие того же проекта не выглядит изменением настроек.
        /// </summary>
        [TestMethod]
        public void ApplyProjectSettings_SameContent_RaisesNothing()
        {
            var store = new AppSettingsStore(new InMemoryPersistedSettings());
            store.ApplyProjectSettings(new GCodeFormatSettings { LineNumberStep = 5 }, null, null, null);
            var raised = 0;
            store.GenerationSettingsChanged += (_, _) => raised++;

            store.ApplyProjectSettings(new GCodeFormatSettings { LineNumberStep = 5 }, null, null, null);

            Assert.AreEqual(0, raised, "То же содержимое — не изменение");
        }

        /// <summary>
        /// Хранилище в памяти: значения по умолчанию нужного типа берутся
        /// из описаний Properties.Settings, как в настоящем user.config.
        /// </summary>
        private sealed class InMemoryPersistedSettings : IPersistedSettings
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public bool UpgradeRequired { get; set; }

            public object this[string name]
            {
                get => _values.TryGetValue(name, out var value) ? value : DefaultFor(name);
                set => _values[name] = value;
            }

            public void Upgrade()
            {
            }

            public void Save()
            {
            }

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
