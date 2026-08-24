using System;
using System.Reflection;
using System.Windows;
using Autofac;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;

namespace GCodeGenerator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartupCore();
        }

        /// <summary>
        /// Composition root (пункт 1.3 плана): замена Mugen <c>Bootstrapper</c>/
        /// <c>BootstrapperEx</c>/<c>GCodeGeneratorMvvmApp</c> и <c>LocalizationModule</c>
        /// на прямой Autofac.
        /// </summary>
        private void StartupCore()
        {
            // Локализация (ранее — LocalizationModule.Load).
            var localizationManager = new AppLocalizationManager();
            localizationManager.AddAssembly("GCodeGenerator");
            LocalizationProvider.Instance = localizationManager;

            // Autofac: регистрация сервисов и view-моделей.
            var builder = new ContainerBuilder();
            builder.RegisterInstance(localizationManager).As<ILocalizationManager>();
            builder.RegisterType<WpfDialogService>().As<IDialogService>().SingleInstance();

            // Пункт 7.5 плана: версия программы через IoC (ранее статика PlatformVariables).
            // Версионирование из git-тега: InformationalVersion = тег (например
            // «1.2.3-rc5»), проставляется при сборке (Directory.Build.targets +
            // build/Get-GitVersion.ps1). Числовая AssemblyVersion (1.2.3.0) для
            // заголовка не подходит — суффикс -alpha/-beta/-rc в ней теряется.
            var versionString = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? "0.1.0-alpha";
            builder.RegisterInstance(new ProgramInfo(versionString)).As<IProgramInfo>().SingleInstance();

            // Пункт 7.5 плана: хранилище настроек через IoC (статический фасад
            // GCodeSettingsStore делегирует тому же экземпляру).
            builder.RegisterInstance(GCodeSettingsStore.Instance).As<ISettingsStore>().SingleInstance();

            // Пункт 7.5 плана: сервис темы через IoC (ранее статика ThemeHelper).
            builder.RegisterType<WpfThemeService>().As<IThemeService>().SingleInstance();

            // Пункт 7.6 плана: служба файлов проекта через IoC (new из VM удалён).
            builder.RegisterType<ProjectFileService>().As<IProjectFileService>().SingleInstance();
            builder.RegisterType<GCodeFileService>().As<IGCodeFileService>().SingleInstance();

            // DXF-парсинг и геометрическое восстановление контуров не являются
            // обязанностью диалоговых ViewModel и доступны через отдельный сервис.
            builder.RegisterType<DxfImportService>().As<IDxfImportService>().SingleInstance();

            // Пункт 7.3 плана: фабрика диалогов редактора операций (реестр
            // Type операции → VM диалога; сверление — по DrillMode).
            builder.RegisterType<OperationEditorFactory>()
                .As<IOperationEditorFactory>()
                .SingleInstance();

            // Пункт 4.5 плана: явная регистрация генераторов G-кода.
            // OperationGeneratorRegistry — явный маппинг Type → IOperationGenerator
            // (name-based рефлексия удалена); SimpleGCodeGenerator резолвит
            // реестр через конструктор и попадает в MainViewModel.
            builder.RegisterType<OperationGeneratorRegistry>()
                .As<IOperationGeneratorRegistry>()
                .SingleInstance();
            builder.RegisterType<SimpleGCodeGenerator>()
                .As<IGCodeGenerator>()
                .SingleInstance();
            builder.RegisterType<GCodeWorkflowFactory>()
                .As<IGCodeWorkflowFactory>()
                .SingleInstance();
            builder.RegisterType<ProjectWorkflowFactory>()
                .As<IProjectWorkflowFactory>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(typeof(MainViewModel).Assembly)
                .AssignableTo<ViewModelBase>()
                .InstancePerDependency();
            var scope = builder.Build();

            // Главное окно (ранее — MvvmApplication.GetStartViewModelType + ShowAsync).
            var mainViewModel = scope.Resolve<MainViewModel>();
            var mainWindow = new MainView { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();

            scope.Resolve<IThemeService>().ApplyTheme(scope.Resolve<ISettingsStore>().Current.Ui.UseDarkTheme);
        }
    }
}
