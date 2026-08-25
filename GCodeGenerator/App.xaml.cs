using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Localization;
using GCodeGenerator.Persistence;
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
        private CrashHandler _crashHandler;
        private MainViewModel _mainViewModel;

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

            // Язык интерфейса — из настроек; пустое значение означает язык
            // системы. Делается до создания окон, чтобы они сразу строились
            // на нужном языке.
            localizationManager.ChangeCulture(
                LanguageChoice.ToCulture(_container.Resolve<ISettingsStore>().Current.Ui.Language));

            // Аварийное сохранение проекта: обработчику сбоя нужны служба
            // файла проекта и каталог рядом с журналом.
            _crashHandler = new CrashHandler(
                _container.Resolve<IProjectFileService>(),
                logger,
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GCodeGenerator",
                    "crash"));

            // Главное окно (ранее — MvvmApplication.GetStartViewModelType + ShowAsync).
            // Ссылка на его view-модель нужна аварийному снимку: документ
            // живёт в ней, а зарегистрирована она как создаваемая заново.
            _mainViewModel = _container.Resolve<MainViewModel>();
            var mainWindow = new MainView { DataContext = _mainViewModel };
            MainWindow = mainWindow;
            mainWindow.Show();

            var uiSettings = _container.Resolve<ISettingsStore>().Current.Ui;
            _container.Resolve<IThemeService>().ApplyTheme(uiSettings.UseDarkTheme);
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
        /// Что делать со сбоем, решает <see cref="CrashHandler"/>: отказ
        /// внешнего ресурса и отменённая операция гасятся, всё остальное
        /// ведёт к аварийному снимку проекта и завершению — работа на
        /// повреждённой модели опаснее, чем остановка. Исключения фоновых
        /// потоков остановить нельзя, они только журналируются.
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

            if (CrashHandler.Classify(e.Exception) == CrashResponse.Continue)
            {
                ShowUnhandledExceptionMessage(e.Exception, null);
                return;
            }

            var snapshotPath = SaveCrashSnapshot();
            ShowUnhandledExceptionMessage(e.Exception, snapshotPath);
            // Код возврата отличает аварийное завершение от обычного выхода.
            Shutdown(1);
        }

        /// <summary>
        /// Снимок документа на момент сбоя — в отдельный файл, а не поверх
        /// проекта пользователя: что именно случилось с моделью, неизвестно.
        /// </summary>
        private string SaveCrashSnapshot()
        {
            var workspace = _mainViewModel?.OperationsWorkspace;
            if (_crashHandler == null || workspace == null)
                return null;

            return _crashHandler.TrySaveSnapshot(
                workspace.AllOperations.ToList(),
                _container?.Resolve<ISettingsStore>()?.Current,
                DateTime.Now);
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
        /// <param name="exception">Исключение, вызвавшее сбой.</param>
        /// <param name="snapshotPath">Путь к аварийному снимку или <c>null</c>.</param>
        private void ShowUnhandledExceptionMessage(Exception exception, string snapshotPath)
        {
            try
            {
                var title = _localizationManager?.GetString("Error") ?? "Error";
                var message = _localizationManager?.GetString(
                    snapshotPath == null ? "UnexpectedErrorMessage" : "FatalErrorMessage")
                    ?? "UnexpectedErrorMessage";
                var logPath = (_logger as FileAppLogger)?.FilePath;
                var details = string.Join(
                    "\n\n",
                    new[] { exception?.Message, snapshotPath, logPath }
                        .Where(part => !string.IsNullOrEmpty(part)));
                MessageBox.Show($"{message}\n\n{details}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception reportingFailure)
            {
                _logger.Error("Не удалось показать сообщение о сбое", reportingFailure);
            }
        }
    }
}
