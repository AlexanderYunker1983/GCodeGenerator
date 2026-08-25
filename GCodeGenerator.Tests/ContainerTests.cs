using System;
using System.Collections.Generic;
using System.Linq;
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
