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
        private readonly IDialogService _dialogService;
        private readonly IGCodeFileService _gCodeFileService;
        private readonly IAppLogger _logger;

        public GCodeWorkflowFactory(
            IGCodeGenerator generator,
            IPostProcessor postProcessor,
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            IGCodeFileService gCodeFileService,
            IAppLogger logger = null)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _postProcessor = postProcessor ?? throw new ArgumentNullException(nameof(postProcessor));
            _localizationManager = localizationManager;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
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
                _dialogService,
                _gCodeFileService,
                _logger);
    }
}
