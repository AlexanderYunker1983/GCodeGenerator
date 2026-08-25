using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;
using GCodeGenerator.Persistence;

namespace GCodeGenerator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private IAppLogger _logger = NullAppLogger.Instance;
        private ILocalizationManager _localizationManager;

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
            // Журнал создаётся первым: он нужен обработчикам необработанных
            // исключений и менеджеру локализации.
            var logger = new FileAppLogger();
            _logger = logger;
            HookUnhandledExceptionHandlers();

            // Локализация (ранее — LocalizationModule.Load).
            var localizationManager = new AppLocalizationManager(logger);
            localizationManager.AddAssembly("GCodeGenerator");
            LocalizationProvider.Instance = localizationManager;
            _localizationManager = localizationManager;

            // Домен знает, что именно не так с параметром, но не знает языка
            // окна: перевод подставляется здесь, один раз на запуск.
            Models.ValidationMessages.Formatter = issue => DescribeValidationIssue(localizationManager, issue);

            // Autofac: регистрация сервисов и view-моделей.
            var builder = new ContainerBuilder();
            builder.RegisterInstance(logger).As<IAppLogger>().SingleInstance();
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

            // Хранилище пользовательских настроек принадлежит IoC-контейнеру;
            // статический compatibility-фасад удалён после переходного релиза.
            builder.RegisterType<AppSettingsStore>().As<ISettingsStore>().SingleInstance();

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

            logger.Info($"Запуск GCodeGenerator {versionString}");

            // Главное окно (ранее — MvvmApplication.GetStartViewModelType + ShowAsync).
            var mainViewModel = scope.Resolve<MainViewModel>();
            var mainWindow = new MainView { DataContext = mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();

            scope.Resolve<IThemeService>().ApplyTheme(scope.Resolve<ISettingsStore>().Current.Ui.UseDarkTheme);
        }

        /// <summary>
        /// Текст проблемы параметра на языке пользователя: ключ выбирается
        /// кодом проблемы, предел подставляется в сообщение.
        /// </summary>
        private static string DescribeValidationIssue(
            Localization.ILocalizationManager localization, Models.ValidationIssue issue)
        {
            if (issue == null)
                return string.Empty;

            var key = $"Validation.{issue.Code}";
            var text = localization?.GetString(key, issue.LimitText);
            // Отсутствующий ключ менеджер возвращает как «?key?» — тогда
            // остаётся английский текст, он понятнее.
            return string.IsNullOrEmpty(text) || text.StartsWith("?", StringComparison.Ordinal)
                ? issue.Message
                : text;
        }

        /// <summary>
        /// Перехват необработанных исключений: до этого любое исключение вне
        /// собственных catch закрывало приложение молча, вместе с несохранённым
        /// проектом.
        ///
        /// Исключение UI-потока журналируется и показывается пользователю, после
        /// чего помечается обработанным: приложение продолжает работу, чтобы
        /// проект можно было сохранить. Исключения фоновых потоков остановить
        /// нельзя — они только журналируются.
        /// </summary>
        private void HookUnhandledExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger.Error("Необработанное исключение в потоке пользовательского интерфейса", e.Exception);
            e.Handled = true;
            ShowUnhandledExceptionMessage(e.Exception);
        }

        private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.Error(
                $"Необработанное исключение вне потока пользовательского интерфейса (IsTerminating={e.IsTerminating})",
                e.ExceptionObject as Exception);
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.Error("Необработанное исключение фоновой задачи", e.Exception);
            e.SetObserved();
        }

        /// <summary>
        /// Сообщение о сбое с путём к журналу. Само по себе не должно приводить
        /// к повторному исключению, поэтому вызов защищён.
        /// </summary>
        private void ShowUnhandledExceptionMessage(Exception exception)
        {
            try
            {
                var title = _localizationManager?.GetString("Error") ?? "Error";
                var message = _localizationManager?.GetString("UnexpectedErrorMessage")
                    ?? "UnexpectedErrorMessage";
                var logPath = (_logger as FileAppLogger)?.FilePath;
                var details = string.IsNullOrEmpty(logPath)
                    ? exception?.Message
                    : $"{exception?.Message}\n\n{logPath}";
                MessageBox.Show($"{message}\n\n{details}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception reportingFailure)
            {
                _logger.Error("Не удалось показать сообщение о сбое", reportingFailure);
            }
        }
    }
}
