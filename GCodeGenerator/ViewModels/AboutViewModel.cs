#nullable enable
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

        public AboutViewModel()
            : this(null, null, null)
        {
        }

        public AboutViewModel(
            ILocalizationManager? localizationManager,
            IProgramInfo? programInfo,
            IShellService? shell)
        {
            _shell = shell;

            DisplayName = localizationManager?.GetString("AboutTitle") ?? "AboutTitle";
            Version = programInfo?.Version ?? string.Empty;
            Copyright = programInfo?.Copyright ?? string.Empty;
            LogFilePath = programInfo?.LogFilePath ?? string.Empty;

            ShowLogCommand = new RelayCommand(
                () => _shell?.ShowFile(LogFilePath),
                () => !string.IsNullOrEmpty(LogFilePath));
            OpenRepositoryCommand = new RelayCommand(() => _shell?.OpenUrl(RepositoryUrl));
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

        public ICommand CloseCommand { get; }
    }
}
