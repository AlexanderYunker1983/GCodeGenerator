using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Autofac;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Сборка приложения контейнером.
    ///
    /// Зависимости view-моделей объявляются в конструкторах, а связываются
    /// модулями контейнера, и рассогласование этих двух мест компилятор не
    /// видит: сервис, забытый в модуле, обнаруживался только при запуске
    /// приложения — окно не открывалось, а причина пряталась во внутреннем
    /// исключении. Поэтому здесь контейнер собирается по-настоящему, из тех
    /// же модулей, что и в App, и создаёт главное окно и каждый диалог.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class ContainerTests
    {
        private static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance(NullAppLogger.Instance).As<IAppLogger>();
            builder.RegisterInstance(new LocalizationManager()).As<ILocalizationManager>();
            builder.RegisterInstance(new ProgramInfo("1.0")).As<IProgramInfo>();
            builder.RegisterModule<CoreServicesModule>();
            builder.RegisterModule<PresentationModule>();
            return builder.Build();
        }

        /// <summary>
        /// Главная view-модель тянет за собой почти всё приложение: рабочие
        /// процессы генерации и проекта, вкладки операций, настройки и темы.
        /// </summary>
        [TestMethod]
        public void Container_ResolvesMainViewModel()
        {
            using (var container = BuildContainer())
            {
                var main = container.Resolve<MainViewModel>();

                Assert.IsNotNull(main);
                Assert.IsNotNull(main.OperationsWorkspace);
            }
        }

        /// <summary>
        /// Диалоги операций фабрика получает по типу view-модели: тип,
        /// зарегистрированный в реестре, но не в контейнере, привёл бы к
        /// отказу открыть окно уже в руках пользователя.
        /// </summary>
        [TestMethod]
        public void Container_ResolvesEveryOperationEditor()
        {
            using (var container = BuildContainer())
            {
                var editors = container.Resolve<Autofac.Features.Indexed.IIndex<Type, IOperationEditorViewModel>>();

                foreach (var viewModelType in EditorViewModelTypes())
                {
                    Assert.IsTrue(editors.TryGetValue(viewModelType, out var editor),
                        $"{viewModelType.Name}: не зарегистрирован в контейнере");
                    Assert.IsInstanceOfType(editor, viewModelType);
                }
            }
        }

        /// <summary>
        /// Окна, которые открываются не по операции: настройки и предпросмотр
        /// траектории. View-модели просят их фабрикой, а не контейнером, и
        /// фабрика тоже должна собираться.
        /// </summary>
        [TestMethod]
        public void Container_ResolvesStandaloneDialogFactories()
        {
            using (var container = BuildContainer())
            {
                Assert.IsNotNull(container.Resolve<Func<SettingsViewModel>>()());
                Assert.IsNotNull(container.Resolve<Func<PreviewViewModel>>()());
            }
        }

        /// <summary>
        /// Журнал доходит до окна предпросмотра.
        ///
        /// Проверяется поле, а не поведение: сбой построения сцены изнутри
        /// собранного контейнером окна не вызвать. Проверять при этом есть
        /// что — журнал объявлен необязательным, как и у прочих служб, и
        /// забытая регистрация не сломала бы ни сборку, ни разрешение:
        /// окно просто молчало бы о сбоях, как молчало до сих пор.
        /// </summary>
        [TestMethod]
        public void Container_GivesThePreviewItsLogger()
        {
            var builder = new ContainerBuilder();
            var logger = new NamedLogger();
            builder.RegisterInstance(logger).As<IAppLogger>();
            builder.RegisterInstance(new LocalizationManager()).As<ILocalizationManager>();
            builder.RegisterInstance(new ProgramInfo("1.0")).As<IProgramInfo>();
            builder.RegisterModule<CoreServicesModule>();
            builder.RegisterModule<PresentationModule>();

            using (var container = builder.Build())
            {
                var preview = container.Resolve<Func<PreviewViewModel>>()();

                var field = typeof(PreviewViewModel).GetField(
                    "_logger", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, "Поле журнала переименовано — проверку нужно обновить");
                Assert.AreSame(logger, field.GetValue(preview),
                    "Окно получило журнал приложения, а не пустышку");
            }
        }

        /// <summary>Журнал, отличимый от пустышки по типу.</summary>
        private sealed class NamedLogger : IAppLogger
        {
            public void Log(LogLevel level, string message, Exception exception = null)
            {
            }
        }

        /// <summary>
        /// Генерация через контейнерный генератор с настройками по умолчанию —
        /// сквозной сценарий кнопки «Сгенерировать G-код». Прямые тесты
        /// генератора собирают реестры сами и этого не проверяют: Autofac
        /// выбирает конструктор с наибольшим числом разрешимых параметров,
        /// коллекцию интерфейса умеет собирать и пустой, и без регистраций
        /// стоек реестр постпроцессоров собирался БЕЗ ЕДИНОЙ СТОЙКИ —
        /// генерация отказывала любым настройкам с пустым перечнем
        /// допустимых, а все прочие тесты оставались зелёными.
        /// </summary>
        [TestMethod]
        public void Container_GeneratesProgramWithDefaultSettings()
        {
            using (var container = BuildContainer())
            {
                var generator = container.Resolve<GCodeGenerator.GCodeGenerators.IGCodeGenerator>();
                var operations = new List<GCodeGenerator.Models.OperationBase>
                {
                    new GCodeGenerator.Models.DrillPointsOperation
                    {
                        Name = "Drill",
                        Holes =
                        {
                            new GCodeGenerator.Models.DrillHole { X = 1, Y = 1, TotalDepth = 2, StepDepth = 1 },
                        },
                    },
                    Fixtures.OperationFixtures.ProfileCircle(),
                    Fixtures.OperationFixtures.PocketCircle(),
                };

                var program = generator.Generate(operations, new GCodeGenerator.Models.GCodeSettings());

                Assert.IsTrue(program.Lines.Count > 0, "Программа построена контейнерной сборкой");
            }
        }

        /// <summary>
        /// Реестр стоек из контейнера полон: обе стойки продукта на месте.
        /// Забытая регистрация стойки делала бы её недоступной только в
        /// приложении — реестр, созданный конструктором по умолчанию в
        /// тестах, её бы по-прежнему содержал.
        /// </summary>
        [TestMethod]
        public void Container_PostProcessorRegistry_ContainsEveryController()
        {
            using (var container = BuildContainer())
            {
                var registry = container.Resolve<GCodeGenerator.GCodeGenerators.IPostProcessorRegistry>();

                var keys = registry.All.Select(postProcessor => postProcessor.Key).ToList();
                CollectionAssert.AreEquivalent(new List<string> { "Generic", "GRBL" }, keys);
            }
        }

        /// <summary>
        /// Три диалоговых контракта разделены, но реализация каждого должна
        /// быть зарегистрирована: без любой из них не собирается ни одна
        /// view-модель, которая ею пользуется.
        /// </summary>
        [TestMethod]
        public void Container_ResolvesEachDialogContract()
        {
            using (var container = BuildContainer())
            {
                Assert.IsInstanceOfType(container.Resolve<IMessageService>(), typeof(WpfMessageService));
                Assert.IsInstanceOfType(container.Resolve<IFileDialogService>(), typeof(WpfFileDialogService));
                Assert.IsInstanceOfType(container.Resolve<IDialogHost>(), typeof(WpfDialogHost));
            }
        }

        private static IEnumerable<Type> EditorViewModelTypes()
            => OperationEditorRegistry.Registrations.Values
                .Concat(OperationEditorRegistry.DrillRegistrations.Values)
                .Distinct();
    }
}
