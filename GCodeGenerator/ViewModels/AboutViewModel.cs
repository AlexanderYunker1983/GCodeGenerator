#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Окно «О программе».
    ///
    /// Прежде его не было вовсе: версию можно было увидеть только в заголовке
    /// главного окна, а путь к журналу работы, лицензию и адрес страницы
    /// продукта пользователю взять было неоткуда — они упоминались лишь
    /// в README, до которого ещё нужно догадаться дойти. Между тем именно эти
    /// сведения просят приложить к сообщению о сбое.
    /// </summary>
    public class AboutViewModel : CloseableViewModel, IHasDisplayName
    {
        /// <summary>Страница продукта: отсюда скачивают новые версии и сюда пишут о проблемах.</summary>
        public const string RepositoryUrl = "https://github.com/AlexanderYunker1983/GCodeGenerator";

        private readonly IShellService? _shell;
        private readonly ILocalizationManager? _localizationManager;
        private readonly IUpdateService? _updates;

        /// <summary>Куда ведёт найденный выпуск; пусто — идти некуда.</summary>
        private string _updatePageUrl = string.Empty;

        private string _updateStatus = string.Empty;
        private bool _isCheckingUpdates;

        public AboutViewModel()
            : this(null, null, null)
        {
        }

        public AboutViewModel(
            ILocalizationManager? localizationManager,
            IProgramInfo? programInfo,
            IShellService? shell)
            : this(localizationManager, programInfo, shell, null)
        {
        }

        /// <summary>Окно «О программе».</summary>
        /// <param name="localizationManager">Словарь интерфейса.</param>
        /// <param name="programInfo">Версия, правообладатель и путь к журналу.</param>
        /// <param name="shell">Показ файла и открытие ссылки.</param>
        /// <param name="updates">
        /// Проверка обновлений по требованию. Нажатие кнопки — это и есть
        /// согласие на обращение к сети, поэтому настройка здесь не спрашивается:
        /// она управляет только проверкой при запуске, которой никто не просил.
        /// </param>
        public AboutViewModel(
            ILocalizationManager? localizationManager,
            IProgramInfo? programInfo,
            IShellService? shell,
            IUpdateService? updates)
        {
            _shell = shell;
            _localizationManager = localizationManager;
            _updates = updates;

            DisplayName = localizationManager?.GetString("AboutTitle") ?? "AboutTitle";
            Version = programInfo?.Version ?? string.Empty;
            Copyright = programInfo?.Copyright ?? string.Empty;
            LogFilePath = programInfo?.LogFilePath ?? string.Empty;

            ShowLogCommand = new RelayCommand(
                () => _shell?.ShowFile(LogFilePath),
                () => !string.IsNullOrEmpty(LogFilePath));
            OpenRepositoryCommand = new RelayCommand(() => _shell?.OpenUrl(RepositoryUrl));
            CheckUpdatesCommand = new AsyncRelayCommand(
                CheckUpdatesAsync,
                () => _updates != null && !IsCheckingUpdates);
            OpenUpdatePageCommand = new RelayCommand(
                () => _shell?.OpenUrl(_updatePageUrl),
                () => _updatePageUrl.Length > 0);
            CloseCommand = new RelayCommand(RequestClose);
        }

        public string DisplayName { get; }

        /// <summary>Версия программы — та же, что в заголовке главного окна.</summary>
        public string Version { get; }

        /// <summary>Правообладатель — то же, что в свойствах файла программы.</summary>
        public string Copyright { get; }

        /// <summary>Где лежит журнал работы: его просят приложить к сообщению о сбое.</summary>
        public string LogFilePath { get; }

        /// <summary>Адрес страницы продукта — показывается и открывается по нажатию.</summary>
        public string Repository => RepositoryUrl;

        /// <summary>Показывает файл журнала в проводнике.</summary>
        public ICommand ShowLogCommand { get; }

        /// <summary>Открывает страницу продукта в браузере.</summary>
        public ICommand OpenRepositoryCommand { get; }

        /// <summary>
        /// Спрашивает у GitHub, не вышла ли версия новее. Нажатие — согласие
        /// на единственное обращение программы к сети.
        /// </summary>
        public ICommand CheckUpdatesCommand { get; }

        /// <summary>Открывает страницу найденного выпуска.</summary>
        public ICommand OpenUpdatePageCommand { get; }

        /// <summary>Чем закончилась проверка; пусто — её ещё не было.</summary>
        public string UpdateStatus
        {
            get => _updateStatus;
            private set
            {
                if (value == _updateStatus) return;
                _updateStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUpdateStatus));
            }
        }

        /// <summary>Проверка что-то сообщила.</summary>
        public bool HasUpdateStatus => _updateStatus.Length > 0;

        /// <summary>Идёт проверка: кнопка на это время недоступна.</summary>
        public bool IsCheckingUpdates
        {
            get => _isCheckingUpdates;
            private set
            {
                if (value == _isCheckingUpdates) return;
                _isCheckingUpdates = value;
                OnPropertyChanged();
                ((IRelayCommand)CheckUpdatesCommand).NotifyCanExecuteChanged();
            }
        }

        /// <summary>Найден выпуск новее установленного.</summary>
        public bool HasNewerVersion => _updatePageUrl.Length > 0;

        public ICommand CloseCommand { get; }

        /// <summary>
        /// Проверяет, не вышла ли новая версия, и говорит об этом словами.
        ///
        /// Отказ проверки — не сбой: сети может не быть, и показывать
        /// исключение за то, что человек нажал «проверить», незачем.
        /// Причина остаётся в журнале, в окне — «проверить не удалось».
        /// </summary>
        private async Task CheckUpdatesAsync()
        {
            if (_updates == null)
                return;

            IsCheckingUpdates = true;
            SetUpdatePage(string.Empty);
            UpdateStatus = Localize("UpdateChecking");
            try
            {
                var answer = await _updates.GetLatestReleaseAsync(CancellationToken.None)
                    .ConfigureAwait(true);
                var installed = ProductVersion.Parse(Version);

                if (answer.Release == null)
                {
                    UpdateStatus = Describe(answer);
                    return;
                }

                if (answer.Release.Version.IsNewerThan(installed))
                {
                    SetUpdatePage(answer.Release.PageUrl);
                    UpdateStatus = UpdateNoticeText.For(_localizationManager, answer.Release.Version.Text);
                    return;
                }

                UpdateStatus = Localize("UpdateUpToDate");
            }
            catch (OperationCanceledException)
            {
                UpdateStatus = Localize("UpdateCheckTimedOut");
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        /// <summary>
        /// Почему проверка не удалась — словами, а не отсылкой к журналу.
        ///
        /// Прежде окно говорило «причина — в журнале работы», и человеку,
        /// нажавшему «проверить», приходилось открывать файл ради одной
        /// строки, которая у программы уже была на руках. Исчерпанный предел
        /// обращений назван отдельно: это не сбой, он проходит сам, и совет
        /// при нём другой.
        /// </summary>
        /// <param name="answer">Чем закончился вопрос о последнем выпуске.</param>
        private string Describe(UpdateCheckResult answer)
        {
            if (answer.IsRateLimited)
                return Localize("UpdateRateLimited");

            return answer.Detail.Length == 0
                ? Localize("UpdateCheckFailed")
                : string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    Localize("UpdateCheckFailedBecause"),
                    answer.Detail);
        }

        /// <summary>Куда ведёт кнопка перехода к выпуску.</summary>
        /// <param name="url">Страница выпуска или пустая строка.</param>
        private void SetUpdatePage(string url)
        {
            _updatePageUrl = url;
            OnPropertyChanged(nameof(HasNewerVersion));
            ((IRelayCommand)OpenUpdatePageCommand).NotifyCanExecuteChanged();
        }

        private string Localize(string key) => _localizationManager?.GetString(key) ?? key;
    }
}
