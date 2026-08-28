using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using GCodeGenerator.GCodeGenerators;
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

            // Подстановки делает и настоящий менеджер: без них проверка
            // прошла бы на шаблоне с «{0}» вместо значения. Шаблон, которому
            // подстановок не досталось, он возвращает как есть — его заполнит
            // тот, кто знает аргументы.
            public string GetString(string key, params object[] parameters)
            {
                if (!Values.TryGetValue(key, out var value))
                    return "?" + key + "?";

                try
                {
                    return string.Format(value, parameters);
                }
                catch (FormatException)
                {
                    return value;
                }
            }

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
        /// Строки словаря на обоих языках продукта.
        ///
        /// Наборы берутся без обращения к родительской культуре: иначе
        /// русский набор молча отдаёт английскую строку из нейтрального,
        /// и проверка перевода не может не пройти — ключ, забытый в русском
        /// файле, она объявила бы переведённым.
        /// </summary>
        /// <param name="keys">Ключи, перевод которых обязателен.</param>
        private static void AssertTranslatedInBothLanguages(params string[] keys)
        {
            var resources = new ResourceManager(
                "GCodeGenerator.Resources.LocalizableResources", typeof(App).Assembly);
            var sets = new Dictionary<string, ResourceSet>
            {
                // Английский — нейтральный набор в самой сборке, русский —
                // сателлит рядом с ней.
                ["en"] = resources.GetResourceSet(CultureInfo.InvariantCulture, true, false),
                ["ru"] = resources.GetResourceSet(CultureInfo.GetCultureInfo("ru"), true, false)
            };

            foreach (var pair in sets)
            {
                Assert.IsNotNull(pair.Value, $"нет набора строк для культуры {pair.Key}");
                foreach (var key in keys)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(pair.Value.GetString(key)),
                        $"нет перевода {key} для культуры {pair.Key}");
                }
            }
        }

        /// <summary>
        /// Ключи словаря существуют для каждого кода ядра — на обоих языках
        /// продукта: код без перевода показывал бы английский текст в русском
        /// интерфейсе, и заметить это можно было бы только вручную.
        /// </summary>
        [TestMethod]
        public void EveryCoreErrorCode_HasTranslationsInBothLanguages()
        {
            AssertTranslatedInBothLanguages(
                typeof(CoreErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Select(field => "CoreError_" + (string)field.GetValue(null))
                    .ToArray());
        }

        // ------------------------------------------------------------------
        // Отказ проверки перед генерацией
        // ------------------------------------------------------------------

        /// <summary>Словарь с переводом всего, что нужно отчёту об отказе.</summary>
        private static DictionaryLocalization RussianReport()
        {
            var localization = new DictionaryLocalization();
            localization.Values["GenerationValidationSettings"] = "Настройки генерации:";
            localization.Values["GenerationValidationOperation"] = "Операция №{0} «{1}»:";
            localization.Values["Validation." + ValidationCode.NotPositive] = "Значение должно быть больше нуля";
            localization.Values["Validation." + ValidationCode.BelowMinimum] = "Значение не может быть меньше {0}";
            localization.Values["Validation." + ValidationCode.NotAllowed] = "Недопустимое значение";
            return localization;
        }

        /// <summary>Отказ с проблемой настроек и двумя проблемами одной операции.</summary>
        private static GCodeGenerationValidationException Failure()
            => new GCodeGenerationValidationException(
                new[]
                {
                    new OperationValidationFailure(1, "Карман", "PocketRectangleOperation", new[]
                    {
                        new ValidationIssue("StepDepth", ValidationCode.NotPositive, "must be greater than zero"),
                        new ValidationIssue("ToolDiameter", ValidationCode.BelowMinimum, "must be at least 0.1", 0.1)
                    })
                },
                new[]
                {
                    new ValidationIssue("PostProcessorName", ValidationCode.NotAllowed, "must be one of generic, grbl")
                });

        /// <summary>
        /// Отказ проверки собирается заново на языке интерфейса: заголовки
        /// настроек и каждой виноватой операции, под ними — её проблемы
        /// с именем параметра, как в диалогах операций.
        /// </summary>
        [TestMethod]
        public void Describe_GenerationFailure_IsBuiltInTheInterfaceLanguage()
        {
            var text = CoreErrorMessages.Describe(Failure(), RussianReport());

            Assert.AreEqual(
                string.Join(Environment.NewLine, new[]
                {
                    "Настройки генерации:",
                    "    PostProcessorName: Недопустимое значение",
                    "Операция №2 «Карман»:",
                    "    StepDepth: Значение должно быть больше нуля",
                    "    ToolDiameter: Значение не может быть меньше 0.1"
                }),
                text);
        }

        /// <summary>
        /// Английский текст самого исключения никуда не делся: он уходит
        /// в журнал, где язык интерфейса значения не имеет, и его же видит
        /// пользователь, если словаря нет вовсе.
        /// </summary>
        [TestMethod]
        public void Describe_GenerationFailure_KeepsEnglishTextForTheLog()
        {
            var failure = Failure();

            StringAssert.Contains(failure.Message, "must be greater than zero", "Журнальный текст цел");
            Assert.AreEqual(failure.Message, CoreErrorMessages.Describe(failure, null),
                "Без словаря показывается сообщение самого отказа");
        }

        /// <summary>
        /// Отсутствующий ключ заголовка не превращает отчёт в «?ключ?»:
        /// отказ — самый редкий путь в программе, и ему нужен образец.
        /// </summary>
        [TestMethod]
        public void Describe_GenerationFailure_FallsBackToEnglishHeadings()
        {
            var localization = new DictionaryLocalization();
            localization.Values["Validation." + ValidationCode.NotPositive] = "Значение должно быть больше нуля";

            var text = CoreErrorMessages.Describe(Failure(), localization);

            StringAssert.Contains(text, "Generation settings:");
            StringAssert.Contains(text, "Operation #2 \"Карман\":");
            StringAssert.Contains(text, "StepDepth: Значение должно быть больше нуля",
                "Переведённые проблемы остаются переведёнными");
            Assert.IsFalse(text.Contains("?GenerationValidation"), "Ключа в тексте нет");
        }

        /// <summary>
        /// Текст одной проблемы — по коду, с подстановкой предела; без
        /// перевода — английский текст самой проблемы. Этот же путь
        /// приложение подставляет в <see cref="ValidationMessages.Formatter"/>,
        /// поэтому диалоги операций и отчёт об отказе говорят одними словами.
        /// </summary>
        [TestMethod]
        public void Describe_ValidationIssue_UsesCodeAndLimit()
        {
            var localization = RussianReport();

            Assert.AreEqual("Значение не может быть меньше 0.1",
                CoreErrorMessages.Describe(
                    new ValidationIssue("ToolDiameter", ValidationCode.BelowMinimum, "must be at least 0.1", 0.1),
                    localization));
            Assert.AreEqual("must not match",
                CoreErrorMessages.Describe(
                    new ValidationIssue("EntryAngle", ValidationCode.Inconsistent, "must not match"), localization),
                "Без перевода — английский текст проблемы");
            Assert.AreEqual(string.Empty, CoreErrorMessages.Describe((ValidationIssue)null, localization));
        }

        /// <summary>
        /// Заголовки отчёта переведены на обоих языках продукта — как и всё
        /// остальное, что видит пользователь.
        /// </summary>
        [TestMethod]
        public void GenerationFailureHeadings_HaveTranslationsInBothLanguages()
        {
            AssertTranslatedInBothLanguages("GenerationValidationSettings", "GenerationValidationOperation");
        }
    }
}
