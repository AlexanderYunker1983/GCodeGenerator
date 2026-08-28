#nullable enable
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Главное окно: сводит вместе рабочую область операций, генерацию
    /// программы и файл проекта.
    ///
    /// Прежде оно пересказывало их свойства и команды своими: два десятка
    /// свойств вида «отдать то, что лежит в подчинённой модели», и подписки,
    /// повторно сообщавшие об изменениях. Разметка привязана к подчинённым
    /// моделям напрямую, поэтому здесь остаётся то, что действительно
    /// принадлежит окну: заголовок, вызов настроек, вопрос при закрытии — и
    /// связи между тремя частями, которые сами друг о друге не знают.
    /// </summary>
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly OperationsWorkspaceViewModel _operationsWorkspace;
        private readonly GCodeWorkflowViewModel _gCodeWorkflow;
        private readonly ProjectWorkflowViewModel _projectWorkflow;
        private readonly ISettingsStore? _settingsStore;
        private readonly ILocalizationManager? _localizationManager;
        private readonly Func<SettingsViewModel> _createSettings;
        private readonly Func<AboutViewModel>? _createAbout;
        private readonly IDialogHost _dialogHost;
        private readonly IProgramInfo _programInfo;
        private readonly IUpdateService? _updates;
        private readonly IShellService? _shell;
        private string _programTitle;
        private string _updateNotice = string.Empty;
        private string _updatePageUrl = string.Empty;
        private IDisposable? _documentBatch;
        private IDisposable? _undoSuspension;
        private string _displayName = string.Empty;

        public MainViewModel(
            ILocalizationManager? localizationManager,
            Func<SettingsViewModel> createSettings,
            IDialogHost dialogHost,
            IGCodeWorkflowFactory gCodeWorkflowFactory,
            IProjectWorkflowFactory projectWorkflowFactory,
            OperationsWorkspaceViewModel operationsWorkspace,
            IProgramInfo programInfo,
            ISettingsStore? settingsStore,
            Func<AboutViewModel>? createAbout = null,
            IUpdateService? updates = null,
            IShellService? shell = null)
        {
            _localizationManager = localizationManager;
            _createSettings = createSettings ?? throw new ArgumentNullException(nameof(createSettings));
            _createAbout = createAbout;
            _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
            _programInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _settingsStore.GenerationSettingsChanged += OnGenerationSettingsChanged;
            _operationsWorkspace = operationsWorkspace
                ?? throw new ArgumentNullException(nameof(operationsWorkspace));

            _gCodeWorkflow = (gCodeWorkflowFactory ?? throw new ArgumentNullException(nameof(gCodeWorkflowFactory)))
                .Create(_operationsWorkspace.AllOperations, _settingsStore.Current);
            _gCodeWorkflow.PropertyChanged += OnGCodeWorkflowPropertyChanged;
            _projectWorkflow = (projectWorkflowFactory ?? throw new ArgumentNullException(nameof(projectWorkflowFactory)))
                .Create(_operationsWorkspace.AllOperations, _gCodeWorkflow);
            _projectWorkflow.ProjectResetting += OnProjectResetting;
            _projectWorkflow.PropertyChanged += OnProjectWorkflowPropertyChanged;
            // Загрузка проекта добавляет операции по одной; предпросмотр
            // собирается один раз в конце, а не после каждой операции.
            _projectWorkflow.DocumentApplying += OnDocumentApplying;
            _projectWorkflow.DocumentApplied += OnDocumentApplied;
            _operationsWorkspace.ContentChanged += OnOperationsWorkspaceContentChanged;

            _updates = updates;
            _shell = shell;

            OpenSettingsCommand = new RelayCommand(OpenSettings);
            OpenAboutCommand = new RelayCommand(OpenAbout, () => _createAbout != null);
            OpenUpdatePageCommand = new RelayCommand(
                () => _shell?.OpenUrl(_updatePageUrl),
                () => _updatePageUrl.Length > 0);

            _programTitle = BuildProgramTitle();
            UpdateDisplayName();

            // Надписи разметки перечитываются при смене языка сами, а
            // заголовок окна собирается здесь — и прежде оставался на языке
            // запуска до перезапуска программы. Обе стороны подписки живут
            // всё время работы приложения, отписка не требуется.
            if (_localizationManager != null)
                _localizationManager.CultureChanged += (_, _) =>
                {
                    _programTitle = BuildProgramTitle();
                    UpdateDisplayName();
                };

            StartUpdateCheck();
        }

        /// <summary>Сообщение о вышедшей версии; пусто — сообщать нечего.</summary>
        public string UpdateNotice
        {
            get => _updateNotice;
            private set
            {
                if (value == _updateNotice) return;
                _updateNotice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUpdate));
            }
        }

        /// <summary>Вышла версия новее установленной.</summary>
        public bool HasUpdate => _updateNotice.Length > 0;

        /// <summary>Открывает страницу вышедшего выпуска.</summary>
        public ICommand OpenUpdatePageCommand { get; }

        /// <summary>
        /// Спрашивает у GitHub, не вышла ли новая версия — но только если
        /// человек об этом просил.
        ///
        /// Настройка выключена по умолчанию: обращение к сети у программы,
        /// работающей с файлами на своём же компьютере, единственное, и
        /// делать его без спроса она не должна. Результат не прерывает
        /// работу — он появляется строкой над списком операций, и её можно
        /// не заметить; тому, кто хочет узнать сейчас, есть кнопка в окне
        /// «О программе».
        /// </summary>
        private void StartUpdateCheck()
        {
            if (_updates == null || _settingsStore?.Current.Ui.CheckForUpdates != true)
                return;

            _ = CheckForUpdateAsync();
        }

        private async Task CheckForUpdateAsync()
        {
            try
            {
                var answer = await _updates!.GetLatestReleaseAsync(CancellationToken.None)
                    .ConfigureAwait(true);

                // Отказ при запуске остаётся в журнале и только там: проверку
                // никто не ждал, и сообщать о её неудаче — значит мешать
                // работе ради того, о чём не спрашивали.
                if (answer.Release == null)
                    return;

                var installed = ProductVersion.Parse(_programInfo.Version);
                if (!answer.Release.Version.IsNewerThan(installed))
                    return;

                _updatePageUrl = answer.Release.PageUrl;
                ((IRelayCommand)OpenUpdatePageCommand).NotifyCanExecuteChanged();
                UpdateNotice = UpdateNoticeText.For(_localizationManager, answer.Release.Version.Text);
            }
            catch (OperationCanceledException)
            {
                // Проверка не уложилась в отведённое время. Молчание здесь —
                // верный ответ: никто её не ждал.
            }
        }

        /// <summary>Название и версия программы на действующем языке.</summary>
        private string BuildProgramTitle()
        {
            var baseTitle = _localizationManager?.GetString("MainTitle") ?? "MainTitle";
            var version = _programInfo.Version;
            return string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";
        }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Рабочая область операций: вкладки, список, выбор и схема.</summary>
        public OperationsWorkspaceViewModel OperationsWorkspace => _operationsWorkspace;

        /// <summary>Генерация программы: ход, текст и его сохранение.</summary>
        public GCodeWorkflowViewModel GCodeWorkflow => _gCodeWorkflow;

        /// <summary>Файл проекта: создание, открытие и сохранение.</summary>
        public ProjectWorkflowViewModel ProjectWorkflow => _projectWorkflow;

        public ICommand OpenSettingsCommand { get; }

        /// <summary>Окно «О программе»: версия, лицензия, журнал работы.</summary>
        public ICommand OpenAboutCommand { get; }

        public void NotifyOperationsChanged()
        {
            _operationsWorkspace.NotifyOperationsChanged();
        }

        /// <summary>
        /// Спрашивает о несохранённом проекте перед закрытием программы.
        /// </summary>
        /// <returns><c>false</c> — закрывать нельзя, пользователь передумал.</returns>
        public bool ConfirmClose() => _projectWorkflow.ConfirmDiscardChanges();

        /// <summary>
        /// Открывает проект из файла: так приходит путь из командной строки
        /// (двойной щелчок по <c>.ygc</c> в проводнике) и из файла,
        /// перетащенного в окно.
        /// </summary>
        /// <param name="fileName">Путь к файлу проекта.</param>
        public Task<bool> OpenProjectAsync(string? fileName)
            => _projectWorkflow.OpenProjectAsync(fileName);

        /// <summary>
        /// Заголовок окна: имя файла проекта, звёздочка при несохранённых
        /// изменениях, затем название и версия программы. Пока проект не
        /// сохранён, вместо имени файла — «без имени».
        /// </summary>
        private void UpdateDisplayName()
        {
            var fileName = string.IsNullOrEmpty(_projectWorkflow.CurrentFileName)
                ? _localizationManager?.GetString("UntitledProject") ?? "UntitledProject"
                : _projectWorkflow.CurrentFileName;
            var changeMark = _projectWorkflow.IsDirty ? "*" : string.Empty;
            DisplayName = $"{fileName}{changeMark} — {_programTitle}";
        }

        private void OnProjectWorkflowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWorkflowViewModel.CurrentPath) ||
                e.PropertyName == nameof(ProjectWorkflowViewModel.IsDirty))
            {
                UpdateDisplayName();
            }
        }

        private void OnGCodeWorkflowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Двумерный предпросмотр умеет показывать саму траекторию, а не
            // только контуры операций: он получает её от той же генерации.
            // Клоны слепка превью разрешает в операции документа сам —
            // по идентификатору операции.
            if (e.PropertyName == nameof(GCodeWorkflowViewModel.GeneratedToolPath))
                _operationsWorkspace.OperationsPreview.ToolPath = _gCodeWorkflow.GeneratedToolPath;
        }

        private void OnOperationsWorkspaceContentChanged(object? sender, EventArgs e)
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
            _projectWorkflow.NotifyOperationsChanged();
        }

        private void OnProjectResetting(object? sender, EventArgs e)
        {
            _operationsWorkspace.SelectedOperation = null;
        }

        private void OnDocumentApplying(object? sender, EventArgs e)
        {
            _documentBatch?.Dispose();
            _documentBatch = _operationsWorkspace.BeginBatchUpdate();

            // Замена документа — не правка, а другой документ: история
            // изменений на её время не пишется, а затем очищается.
            _undoSuspension?.Dispose();
            _undoSuspension = _operationsWorkspace.History.SuspendAndClear();
        }

        private void OnDocumentApplied(object? sender, EventArgs e)
        {
            _documentBatch?.Dispose();
            _documentBatch = null;
            _undoSuspension?.Dispose();
            _undoSuspension = null;
        }

        private void OnGenerationSettingsChanged(object? sender, EventArgs e)
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
            // Настройки генерации сохраняются вместе с проектом, поэтому их
            // правка делает проект несохранённым. Событие приходит только при
            // фактическом их изменении: смена темы или языка программу не
            // сбрасывает и проект не пачкает.
            _projectWorkflow.MarkDirty();
        }

        private void OpenSettings()
        {
            _dialogHost.ShowDialog(_createSettings());
        }

        private void OpenAbout()
        {
            if (_createAbout != null)
                _dialogHost.ShowDialog(_createAbout());
        }
    }
}
