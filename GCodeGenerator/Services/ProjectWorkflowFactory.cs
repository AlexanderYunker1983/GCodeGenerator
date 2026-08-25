#nullable enable
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
        private readonly ILocalizationManager? _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsStore? _settingsStore;
        private readonly IProjectFileService _projectFileService;
        private readonly IAppLogger _logger;

        public ProjectWorkflowFactory(
            ILocalizationManager? localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            ISettingsStore? settingsStore,
            IProjectFileService projectFileService,
            IAppLogger? logger = null)
        {
            _localizationManager = localizationManager;
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
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
                _messageService,
                _fileDialogService,
                _settingsStore,
                _projectFileService,
                _logger);
    }
}
