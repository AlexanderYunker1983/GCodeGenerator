using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System;
using System.Collections.ObjectModel;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Services
{
    public sealed class ProjectWorkflowFactory : IProjectWorkflowFactory
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly ISettingsStore _settingsStore;
        private readonly IProjectFileService _projectFileService;
        private readonly IAppLogger _logger;

        public ProjectWorkflowFactory(
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            ISettingsStore settingsStore,
            IProjectFileService projectFileService,
            IAppLogger logger = null)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));
            _logger = logger ?? NullAppLogger.Instance;
        }

        public ProjectWorkflowViewModel Create(
            ObservableCollection<OperationBase> operations,
            GCodeWorkflowViewModel gCodeWorkflow)
            => new ProjectWorkflowViewModel(
                operations,
                gCodeWorkflow,
                _localizationManager,
                _dialogService,
                _settingsStore,
                _projectFileService,
                _logger);
    }
}
