using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Owns the generated G-code lifecycle: background generation, progress,
    /// stale-result invalidation, preview and file saving.
    /// </summary>
    public sealed class GCodeWorkflowViewModel : ViewModelBase
    {
        private readonly IList<OperationBase> _operations;
        private readonly GCodeSettings _settings;
        private readonly IGCodeGenerator _generator;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IGCodeFileService _gCodeFileService;
        private readonly IAppLogger _logger;
        private GCodeProgram _generatedProgram;
        private long _documentRevision;
        private string _gCodePreview;
        private bool _isGenerating;
        private int _progressPercent;

        internal GCodeWorkflowViewModel(
            IList<OperationBase> operations,
            GCodeSettings settings,
            IGCodeGenerator generator,
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            IGCodeFileService gCodeFileService,
            IAppLogger logger = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _localizationManager = localizationManager;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _gCodeFileService = gCodeFileService ?? throw new ArgumentNullException(nameof(gCodeFileService));
            _logger = logger ?? NullAppLogger.Instance;

            GenerateGCodeCommand = new AsyncRelayCommand(GenerateGCodeAsync, () => _operations.Count > 0);
            SaveGCodeCommand = new RelayCommand(SaveGCode, () => !string.IsNullOrEmpty(GCodePreview));
            PreviewGCodeCommand = new RelayCommand(PreviewGCode, () => !string.IsNullOrEmpty(GCodePreview));
        }

        public string GCodePreview
        {
            get => _gCodePreview;
            set
            {
                if (Equals(value, _gCodePreview)) return;
                _gCodePreview = value;
                OnPropertyChanged();
                ((RelayCommand)SaveGCodeCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviewGCodeCommand).NotifyCanExecuteChanged();
            }
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            private set
            {
                if (value == _isGenerating) return;
                _isGenerating = value;
                OnPropertyChanged();
            }
        }

        public int ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                if (value == _progressPercent) return;
                _progressPercent = value;
                OnPropertyChanged();
            }
        }

        public ICommand GenerateGCodeCommand { get; }

        public ICommand SaveGCodeCommand { get; }

        public ICommand PreviewGCodeCommand { get; }

        public void InvalidateGeneratedProgram()
        {
            Interlocked.Increment(ref _documentRevision);
            _generatedProgram = null;
            GCodePreview = string.Empty;
            ((IRelayCommand)GenerateGCodeCommand).NotifyCanExecuteChanged();
        }

        private async Task GenerateGCodeAsync()
        {
            if (IsGenerating)
                return;

            IsGenerating = true;
            ProgressPercent = 0;
            _generatedProgram = null;
            GCodePreview = string.Empty;
            var generationRevision = Volatile.Read(ref _documentRevision);
            var generationCompleted = false;
            try
            {
                var operations = new List<OperationBase>(_operations);
                var progress = new Progress<int>(p =>
                {
                    if (generationRevision == Volatile.Read(ref _documentRevision))
                        ProgressPercent = p;
                });
                var program = await Task.Run(() =>
                    _generator.Generate(operations, _settings, progress));

                if (generationRevision != Volatile.Read(ref _documentRevision))
                {
                    ProgressPercent = 0;
                    _logger.Info("G-code generation result discarded: operations changed while generating");
                    return;
                }

                _generatedProgram = program;
                var text = new StringBuilder();
                foreach (var line in program.Lines)
                    text.AppendLine(line);
                GCodePreview = text.ToString();
                generationCompleted = true;
                _logger.Info($"G-code generated: {operations.Count} operation(s), {program.Lines.Count} line(s)");
            }
            catch (Exception ex)
            {
                _generatedProgram = null;
                GCodePreview = string.Empty;
                ProgressPercent = 0;
                _logger.Error("G-code generation failed", ex);
                var message = _localizationManager?.GetString("ErrorGeneratingGCode") ?? "ErrorGeneratingGCode";
                var errorTitle = _localizationManager?.GetString("Error") ?? "Error";
                _dialogService.ShowError($"{message}\n{ex.Message}", errorTitle);
            }
            finally
            {
                IsGenerating = false;
                if (generationCompleted)
                    ProgressPercent = 100;
            }
        }

        private void SaveGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            var fileName = _dialogService.ShowSaveDialog(
                "",
                "G-code files (*.nc;*.tap)|*.nc;*.tap|NC files (*.nc)|*.nc|TAP files (*.tap)|*.tap|All files (*.*)|*.*",
                "nc",
                "program.nc");
            if (fileName == null)
                return;

            try
            {
                _gCodeFileService.Save(fileName, GCodePreview);
                _logger.Info($"G-code saved: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Saving G-code failed: {fileName}", ex);
                var message = _localizationManager?.GetString("ErrorSavingGCodeFile") ?? "ErrorSavingGCodeFile";
                var errorTitle = _localizationManager?.GetString("Error") ?? "Error";
                _dialogService.ShowError($"{message}\n{ex.Message}", errorTitle);
            }
        }

        private void PreviewGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            var viewModel = _dialogService.CreateViewModel<PreviewViewModel>();
            viewModel.Program = _generatedProgram;
            _dialogService.ShowDialog(viewModel);
        }
    }
}
