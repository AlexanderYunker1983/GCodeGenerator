using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Перевод отказов ядра на границе интерфейса. Ядро несёт код и
    /// аргументы, интерфейс подставляет шаблон из словаря; прежде исключения
    /// персистентности и импорта были захардкожены по-русски и показывались
    /// как есть — пользователь с английским интерфейсом получал русский
    /// текст, а перевести его без правки ядра было нельзя.
    /// </summary>
    [TestClass]
    public class CoreErrorMessagesTests
    {
        private sealed class DictionaryLocalization : ILocalizationManager
        {
            public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

            public string GetString(string key, params object[] parameters)
                => Values.TryGetValue(key, out var value) ? value : "?" + key + "?";

            public void ChangeCulture(CultureInfo cultureInfo)
            {
            }

            public void AddResourceManager(ResourceManager resourceManager)
            {
            }

            public void AddAssembly(Assembly assembly, string resourcePath = "Resources.LocalizableResources")
            {
            }

            public void AddAssembly(string assemblyName, string resourcePath = "Resources.LocalizableResources")
            {
            }

            public event EventHandler CultureChanged
            {
                add { }
                remove { }
            }
        }

        /// <summary>
        /// Отказ с кодом переводится по словарю, аргументы подставляются
        /// в переведённый шаблон.
        /// </summary>
        [TestMethod]
        public void Describe_TranslatesCoreExceptionByCode()
        {
            var localization = new DictionaryLocalization();
            localization.Values["CoreError_" + CoreErrorCodes.ProjectFileUnknownSection] =
                "В файле проекта неизвестная секция «{0}».";
            var failure = new CoreException(CoreErrorCodes.ProjectFileUnknownSection,
                "The project file contains an unknown section '{0}'.", "futureData");

            var text = CoreErrorMessages.Describe(failure, localization);

            Assert.AreEqual("В файле проекта неизвестная секция «futureData».", text);
        }

        /// <summary>
        /// Без перевода — нейтральное сообщение самого отказа: и для кода,
        /// которого нет в словаре, и без словаря вовсе, и для обычных
        /// исключений без кода.
        /// </summary>
        [TestMethod]
        public void Describe_FallsBackToNeutralMessage()
        {
            var failure = new CoreException("NoSuchCode", "Neutral text {0}.", 7);

            Assert.AreEqual("Neutral text 7.", CoreErrorMessages.Describe(failure, new DictionaryLocalization()));
            Assert.AreEqual("Neutral text 7.", CoreErrorMessages.Describe(failure, null));
            Assert.AreEqual("plain", CoreErrorMessages.Describe(new InvalidOperationException("plain"), null));
        }

        /// <summary>
        /// Ключи словаря существуют для каждого кода ядра — на обоих языках
        /// продукта: код без перевода показывал бы английский текст в русском
        /// интерфейсе, и заметить это можно было бы только вручную.
        /// </summary>
        [TestMethod]
        public void EveryCoreErrorCode_HasTranslationsInBothLanguages()
        {
            var resources = new ResourceManager(
                "GCodeGenerator.Resources.LocalizableResources", typeof(App).Assembly);

            foreach (var field in typeof(CoreErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var key = "CoreError_" + (string)field.GetValue(null);
                foreach (var culture in new[] { "en", "ru" })
                {
                    var value = resources.GetString(key, CultureInfo.GetCultureInfo(culture));
                    Assert.IsFalse(string.IsNullOrWhiteSpace(value),
                        $"нет перевода {key} для культуры {culture}");
                }
            }
        }
    }
}
