using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly OperationsWorkspaceViewModel _operationsWorkspace;
        private readonly GCodeWorkflowViewModel _gCodeWorkflow;
        private readonly ProjectWorkflowViewModel _projectWorkflow;
        private readonly ISettingsStore _settingsStore;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IProgramInfo _programInfo;
        private readonly string _programTitle;
        private string _displayName;

        public MainViewModel(
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            IGCodeWorkflowFactory gCodeWorkflowFactory,
            IProjectWorkflowFactory projectWorkflowFactory,
            OperationsWorkspaceViewModel operationsWorkspace,
            IProgramInfo programInfo,
            ISettingsStore settingsStore)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            _programInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _settingsStore.SettingsChanged += OnSettingsChanged;
            _operationsWorkspace = operationsWorkspace
                ?? throw new ArgumentNullException(nameof(operationsWorkspace));

            _gCodeWorkflow = (gCodeWorkflowFactory ?? throw new ArgumentNullException(nameof(gCodeWorkflowFactory)))
                .Create(AllOperations, _settingsStore.Current);
            _gCodeWorkflow.PropertyChanged += OnGCodeWorkflowPropertyChanged;
            _projectWorkflow = (projectWorkflowFactory ?? throw new ArgumentNullException(nameof(projectWorkflowFactory)))
                .Create(AllOperations, _gCodeWorkflow);
            _projectWorkflow.ProjectResetting += OnProjectResetting;
            _projectWorkflow.PropertyChanged += OnProjectWorkflowPropertyChanged;
            _operationsWorkspace.PropertyChanged += OnOperationsWorkspacePropertyChanged;
            _operationsWorkspace.ContentChanged += OnOperationsWorkspaceContentChanged;

            GenerateGCodeCommand = _gCodeWorkflow.GenerateGCodeCommand;
            SaveGCodeCommand = _gCodeWorkflow.SaveGCodeCommand;
            PreviewGCodeCommand = _gCodeWorkflow.PreviewGCodeCommand;
            NewProgramCommand = _projectWorkflow.NewProgramCommand;
            SaveProjectCommand = _projectWorkflow.SaveProjectCommand;
            SaveProjectAsCommand = _projectWorkflow.SaveProjectAsCommand;
            OpenProjectCommand = _projectWorkflow.OpenProjectCommand;
            ShowAllPreviewCommand = _operationsWorkspace.ShowAllPreviewCommand;
            MoveOperationUpCommand = _operationsWorkspace.MoveOperationUpCommand;
            MoveOperationDownCommand = _operationsWorkspace.MoveOperationDownCommand;
            RemoveOperationCommand = _operationsWorkspace.RemoveOperationCommand;
            EditOperationCommand = _operationsWorkspace.EditOperationCommand;
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

        public DrillOperationsViewModel DrillOperations => _operationsWorkspace.DrillOperations;

        public ProfileMillingOperationsViewModel ProfileMillingOperations
            => _operationsWorkspace.ProfileMillingOperations;

        public PocketOperationsViewModel PocketOperations => _operationsWorkspace.PocketOperations;

        public OperationsPreviewViewModel OperationsPreview => _operationsWorkspace.OperationsPreview;

        public ObservableCollection<OperationBase> AllOperations => _operationsWorkspace.AllOperations;

        public OperationBase SelectedOperation
        {
            get => _operationsWorkspace.SelectedOperation;
            set => _operationsWorkspace.SelectedOperation = value;
        }

        public string GCodePreview
        {
            get => _gCodeWorkflow.GCodePreview;
            set => _gCodeWorkflow.GCodePreview = value;
        }

        public bool IsGenerating => _gCodeWorkflow.IsGenerating;

        public int ProgressPercent => _gCodeWorkflow.ProgressPercent;

        public ICommand GenerateGCodeCommand { get; }

        public ICommand SaveGCodeCommand { get; }

        public ICommand PreviewGCodeCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand ShowAllPreviewCommand { get; }

        public ICommand MoveOperationUpCommand { get; }

        public ICommand MoveOperationDownCommand { get; }

        public ICommand RemoveOperationCommand { get; }

        public ICommand EditOperationCommand { get; }

        public ICommand NewProgramCommand { get; }

        public ICommand SaveProjectCommand { get; }

        public ICommand SaveProjectAsCommand { get; }

        public ICommand OpenProjectCommand { get; }

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
            if (e.PropertyName == nameof(GCodeWorkflowViewModel.GCodePreview) ||
                e.PropertyName == nameof(GCodeWorkflowViewModel.IsGenerating) ||
                e.PropertyName == nameof(GCodeWorkflowViewModel.ProgressPercent))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }

        private void OnOperationsWorkspacePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OperationsWorkspaceViewModel.SelectedOperation))
                OnPropertyChanged(nameof(SelectedOperation));
        }

        private void OnOperationsWorkspaceContentChanged(object sender, EventArgs e)
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
            _projectWorkflow.NotifyOperationsChanged();
        }

        private void OnProjectResetting(object sender, EventArgs e)
        {
            SelectedOperation = null;
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
            var viewModel = _dialogService.CreateViewModel<SettingsViewModel>();
            _dialogService.ShowDialog(viewModel);
        }
    }
}
