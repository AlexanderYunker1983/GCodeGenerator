using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Localization;
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
        private IAppLogger _logger = NullAppLogger.Instance;
        private ILocalizationManager _localizationManager;
        private IContainer _container;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            StartupCore();
        }

        /// <summary>
        /// Освобождает контейнер при выходе: службы, которым есть что закрыть,
        /// должны об этом узнать.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _container?.Dispose();
            _container = null;
            base.OnExit(e);
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

            // Autofac: регистрации разнесены по модулям — службы отдельно,
            // интерфейс отдельно; здесь остаётся только то, что существует
            // в единственном экземпляре и создаётся до контейнера.
            var builder = new ContainerBuilder();
            builder.RegisterInstance(logger).As<IAppLogger>().SingleInstance();
            builder.RegisterInstance(localizationManager).As<ILocalizationManager>();
            builder.RegisterModule<Infrastructure.CoreServicesModule>();
            builder.RegisterModule<Infrastructure.PresentationModule>();

            // Пункт 7.5 плана: версия программы через IoC (ранее статика PlatformVariables).
            // Версионирование из git-тега: InformationalVersion = тег (например
            // «1.2.3-rc5»), проставляется при сборке (Directory.Build.targets +
            // build/Get-GitVersion.ps1). Числовая AssemblyVersion (1.2.3.0) для
            // заголовка не подходит — суффикс -alpha/-beta/-rc в ней теряется.
            var versionString = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? "0.1.0-alpha";
            builder.RegisterInstance(new ProgramInfo(versionString)).As<IProgramInfo>().SingleInstance();

            // Контейнер живёт столько же, сколько приложение, и освобождается
            // при выходе: прежде он оставался безымянной переменной, поэтому
            // службы, которым есть что закрыть, об этом не узнавали.
            _container = builder.Build();

            logger.Info($"Запуск GCodeGenerator {versionString}");

            // Главное окно (ранее — MvvmApplication.GetStartViewModelType + ShowAsync).
            var mainWindow = new MainView { DataContext = _container.Resolve<MainViewModel>() };
            MainWindow = mainWindow;
            mainWindow.Show();

            _container.Resolve<IThemeService>()
                .ApplyTheme(_container.Resolve<ISettingsStore>().Current.Ui.UseDarkTheme);
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
