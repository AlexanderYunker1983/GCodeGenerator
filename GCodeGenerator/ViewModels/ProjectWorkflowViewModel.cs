using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Owns the user workflow for creating, opening and saving a project.
    /// The operation collection keeps its identity so all category views stay bound.
    /// </summary>
    public sealed class ProjectWorkflowViewModel
    {
        private readonly ObservableCollection<OperationBase> _operations;
        private readonly GCodeWorkflowViewModel _gCodeWorkflow;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly ISettingsStore _settingsStore;
        private readonly IProjectFileService _projectFileService;
        private readonly IAppLogger _logger;

        internal ProjectWorkflowViewModel(
            ObservableCollection<OperationBase> operations,
            GCodeWorkflowViewModel gCodeWorkflow,
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            ISettingsStore settingsStore,
            IProjectFileService projectFileService,
            IAppLogger logger = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _gCodeWorkflow = gCodeWorkflow ?? throw new ArgumentNullException(nameof(gCodeWorkflow));
            _localizationManager = localizationManager;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));
            _logger = logger ?? NullAppLogger.Instance;

            NewProgramCommand = new RelayCommand(CreateNewProgram);
            SaveProjectCommand = new RelayCommand(SaveProject, () => _operations.Count > 0);
            OpenProjectCommand = new RelayCommand(OpenProject);
        }

        public event EventHandler ProjectResetting;

        public ICommand NewProgramCommand { get; }

        public ICommand SaveProjectCommand { get; }

        public ICommand OpenProjectCommand { get; }

        public void NotifyOperationsChanged()
        {
            ((RelayCommand)SaveProjectCommand).NotifyCanExecuteChanged();
        }

        private void CreateNewProgram()
        {
            if (!HasCurrentContent())
                return;

            var message = Localize("ConfirmNewProjectMessage");
            var title = Localize("ConfirmNewProjectTitle");
            if (!_dialogService.ShowConfirm(message, title))
                return;

            ResetOperations();
            _settingsStore.RestoreGlobalGenerationSettings();
        }

        private void SaveProject()
        {
            if (_operations.Count == 0)
                return;

            var filter = Localize("ProjectFileFilter");
            var title = Localize("SaveProjectTitle");
            var fileName = _dialogService.ShowSaveDialog(title, filter, "ygc", "project.ygc");
            if (fileName == null)
                return;

            try
            {
                _projectFileService.Save(fileName, _operations, _settingsStore.Current);
                _logger.Info($"Project saved: {fileName} ({_operations.Count} operation(s))");
            }
            catch (Exception ex)
            {
                _logger.Error($"Saving project failed: {fileName}", ex);
                var message = Localize("ErrorSavingProject");
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private void OpenProject()
        {
            if (!ConfirmResetIfNeeded())
                return;

            var filter = Localize("ProjectFileFilter");
            var title = Localize("OpenProjectTitle");
            var fileName = _dialogService.ShowOpenDialog(title, filter, "ygc");
            if (fileName == null)
                return;

            try
            {
                var data = _projectFileService.Load(fileName);
                if (data?.Operations == null)
                {
                    _logger.Warning($"Project file has no operations section: {fileName}");
                    _dialogService.ShowError(Localize("InvalidProjectFile"), title);
                    return;
                }

                ApplyProjectSettings(data);
                ResetOperations();
                foreach (var operation in data.Operations)
                    _operations.Add(operation);
                _logger.Info($"Project opened: {fileName} ({data.Operations.Count} operation(s))");
            }
            catch (Exception ex)
            {
                _logger.Error($"Opening project failed: {fileName}", ex);
                var message = Localize("ErrorOpeningProject");
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private bool ConfirmResetIfNeeded()
        {
            if (!HasCurrentContent())
                return true;

            return _dialogService.ShowConfirm(
                Localize("ConfirmNewProjectMessage"),
                Localize("ConfirmNewProjectTitle"));
        }

        private bool HasCurrentContent()
            => _operations.Count > 0 || !string.IsNullOrWhiteSpace(_gCodeWorkflow.GCodePreview);

        private void ResetOperations()
        {
            ProjectResetting?.Invoke(this, EventArgs.Empty);
            _gCodeWorkflow.InvalidateGeneratedProgram();
            _operations.Clear();
        }

        private void ApplyProjectSettings(ProjectFileData data)
        {
            _settingsStore.RestoreGlobalGenerationSettings();
            var settings = _settingsStore.Current;
            if (data.Format != null)
                settings.Format = data.Format;
            if (data.Spindle != null)
                settings.Spindle = data.Spindle;
            if (data.Coolant != null)
                settings.Coolant = data.Coolant;
            if (data.WorkCoordinate != null)
                settings.WorkCoordinate = data.WorkCoordinate;
        }

        private string Localize(string key)
            => _localizationManager?.GetString(key) ?? key;
    }
}
