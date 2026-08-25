using GCodeGenerator.Diagnostics;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System;
using System.Collections.Generic;

namespace GCodeGenerator.Services
{
    public sealed class GCodeWorkflowFactory : IGCodeWorkflowFactory
    {
        private readonly IGCodeGenerator _generator;
        private readonly IPostProcessor _postProcessor;
        private readonly ILocalizationManager _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly Func<PreviewViewModel> _createPreview;
        private readonly IDialogHost _dialogHost;
        private readonly IGCodeFileService _gCodeFileService;
        private readonly IAppLogger _logger;

        public GCodeWorkflowFactory(
            IGCodeGenerator generator,
            IPostProcessor postProcessor,
            ILocalizationManager localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            Func<PreviewViewModel> createPreview,
            IDialogHost dialogHost,
            IGCodeFileService gCodeFileService,
            IAppLogger logger = null)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _postProcessor = postProcessor ?? throw new ArgumentNullException(nameof(postProcessor));
            _localizationManager = localizationManager;
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _createPreview = createPreview ?? throw new ArgumentNullException(nameof(createPreview));
            _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
            _gCodeFileService = gCodeFileService ?? throw new ArgumentNullException(nameof(gCodeFileService));
            _logger = logger ?? NullAppLogger.Instance;
        }

        public GCodeWorkflowViewModel Create(IList<OperationBase> operations, GCodeSettings settings)
            => new GCodeWorkflowViewModel(
                operations,
                settings,
                _generator,
                _postProcessor,
                _localizationManager,
                _messageService,
                _fileDialogService,
                _createPreview,
                _dialogHost,
                _gCodeFileService,
                _logger);
    }
}
