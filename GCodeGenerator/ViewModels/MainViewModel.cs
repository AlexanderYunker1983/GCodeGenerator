using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;
using System;
using System.ComponentModel;
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
        private readonly ISettingsStore _settingsStore;
        private readonly ILocalizationManager _localizationManager;
        private readonly Func<SettingsViewModel> _createSettings;
        private readonly IDialogHost _dialogHost;
        private readonly IProgramInfo _programInfo;
        private readonly string _programTitle;
        private IDisposable _documentBatch;
        private string _displayName;

        public MainViewModel(
            ILocalizationManager localizationManager,
            Func<SettingsViewModel> createSettings,
            IDialogHost dialogHost,
            IGCodeWorkflowFactory gCodeWorkflowFactory,
            IProjectWorkflowFactory projectWorkflowFactory,
            OperationsWorkspaceViewModel operationsWorkspace,
            IProgramInfo programInfo,
            ISettingsStore settingsStore)
        {
            _localizationManager = localizationManager;
            _createSettings = createSettings ?? throw new ArgumentNullException(nameof(createSettings));
            _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
            _programInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _settingsStore.SettingsChanged += OnSettingsChanged;
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

            OpenSettingsCommand = new RelayCommand(OpenSettings);

            var baseTitle = _localizationManager?.GetString("MainTitle") ?? "MainTitle";
            var version = _programInfo.Version;
            _programTitle = string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";
            UpdateDisplayName();
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

        private void OnProjectWorkflowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectWorkflowViewModel.CurrentPath) ||
                e.PropertyName == nameof(ProjectWorkflowViewModel.IsDirty))
            {
                UpdateDisplayName();
            }
        }

        private void OnGCodeWorkflowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Двумерный предпросмотр умеет показывать саму траекторию, а не
            // только контуры операций: он получает её от той же генерации.
            if (e.PropertyName == nameof(GCodeWorkflowViewModel.GeneratedToolPath))
                _operationsWorkspace.OperationsPreview.ToolPath = _gCodeWorkflow.GeneratedToolPath;

        }

        private void OnOperationsWorkspaceContentChanged(object sender, EventArgs e)
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
            _projectWorkflow.NotifyOperationsChanged();
        }

        private void OnProjectResetting(object sender, EventArgs e)
        {
            _operationsWorkspace.SelectedOperation = null;
        }

        private void OnDocumentApplying(object sender, EventArgs e)
        {
            _documentBatch?.Dispose();
            _documentBatch = _operationsWorkspace.BeginBatchUpdate();
        }

        private void OnDocumentApplied(object sender, EventArgs e)
        {
            _documentBatch?.Dispose();
            _documentBatch = null;
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
            // Настройки генерации сохраняются вместе с проектом, поэтому их
            // правка делает проект несохранённым.
            _projectWorkflow.MarkDirty();
        }

        private void OpenSettings()
        {
            _dialogHost.ShowDialog(_createSettings());
        }
    }
}
