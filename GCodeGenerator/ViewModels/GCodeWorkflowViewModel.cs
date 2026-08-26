#nullable enable
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using System;
using System.Collections.Generic;
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
        private readonly IPostProcessorRegistry _postProcessors;
        private readonly ILocalizationManager? _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly Func<PreviewViewModel> _createPreview;
        private readonly IDialogHost _dialogHost;
        private readonly IGCodeFileService _gCodeFileService;
        private readonly IAppLogger _logger;
        private Toolpath.ToolPath? _generatedToolPath;

        /// <summary>
        /// Отмена текущей генерации. Документ мог измениться, пока строилась
        /// программа: её результат всё равно будет отброшен, поэтому работу
        /// незачем доводить до конца.
        /// </summary>
        private CancellationTokenSource? _generationCancellation;
        private long _documentRevision;
        private IReadOnlyList<string>? _programLines;
        private bool _isGenerating;
        private int _progressPercent;

        internal GCodeWorkflowViewModel(
            IList<OperationBase> operations,
            GCodeSettings settings,
            IGCodeGenerator generator,
            IPostProcessorRegistry postProcessors,
            ILocalizationManager? localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            Func<PreviewViewModel> createPreview,
            IDialogHost dialogHost,
            IGCodeFileService gCodeFileService,
            IAppLogger? logger = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _postProcessors = postProcessors ?? throw new ArgumentNullException(nameof(postProcessors));
            _localizationManager = localizationManager;
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _createPreview = createPreview ?? throw new ArgumentNullException(nameof(createPreview));
            _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
            _gCodeFileService = gCodeFileService ?? throw new ArgumentNullException(nameof(gCodeFileService));
            _logger = logger ?? NullAppLogger.Instance;

            GenerateGCodeCommand = new AsyncRelayCommand(GenerateGCodeAsync, () => _operations.Count > 0);
            SaveGCodeCommand = new RelayCommand(SaveGCode, () => ProgramLines is { Count: > 0 });
            PreviewGCodeCommand = new RelayCommand(PreviewGCode, () => ProgramLines is { Count: > 0 });
        }

        /// <summary>
        /// Строки построенной программы; null — программы нет. Предпросмотр
        /// показывает их виртуализированным списком: сто тысяч строк не
        /// склеиваются в многомегабайтный текст поля ввода. Запись — для
        /// тестов, которым нужно готовое состояние «программа построена».
        /// </summary>
        public IReadOnlyList<string>? ProgramLines
        {
            get => _programLines;
            internal set
            {
                if (ReferenceEquals(value, _programLines)) return;
                _programLines = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GCodePreview));
                ((RelayCommand)SaveGCodeCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviewGCodeCommand).NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Текст программы целиком, с завершающим переводом строки. Собирается
        /// по требованию — при сохранении в файл; постоянно полная строка
        /// нигде не хранится.
        /// </summary>
        public string GCodePreview
            => _programLines is { Count: > 0 } lines
                ? string.Join(Environment.NewLine, lines) + Environment.NewLine
                : string.Empty;

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

        /// <summary>
        /// Траектория последней успешной генерации: её показывают оба
        /// предпросмотра — трёхмерный целиком, двумерный видом сверху.
        /// </summary>
        public Toolpath.ToolPath? GeneratedToolPath
        {
            get => _generatedToolPath;
            private set
            {
                if (ReferenceEquals(value, _generatedToolPath)) return;
                _generatedToolPath = value;
                OnPropertyChanged();
            }
        }

        public ICommand GenerateGCodeCommand { get; }

        public ICommand SaveGCodeCommand { get; }

        public ICommand PreviewGCodeCommand { get; }

        public void InvalidateGeneratedProgram()
        {
            Interlocked.Increment(ref _documentRevision);
            _generationCancellation?.Cancel();
            GeneratedToolPath = null;
            ProgramLines = null;
            ((IRelayCommand)GenerateGCodeCommand).NotifyCanExecuteChanged();
        }

        private async Task GenerateGCodeAsync()
        {
            if (IsGenerating)
                return;

            IsGenerating = true;
            ProgressPercent = 0;
            GeneratedToolPath = null;
            ProgramLines = null;
            var generationRevision = Volatile.Read(ref _documentRevision);
            var generationCompleted = false;
            var cancellation = new CancellationTokenSource();
            _generationCancellation = cancellation;
            try
            {
                // Слепок сериализуется на потоке интерфейса — документ нельзя
                // читать из фона, пока его может править пользователь, — а
                // дорогая материализация копий уходит в фон: окно держится
                // ровно столько, сколько занимает сериализация.
                var payload = GenerationSnapshot.Serialize(_operations, _settings);
                var operationCount = _operations.Count;

                var progress = new Progress<int>(p =>
                {
                    if (generationRevision == Volatile.Read(ref _documentRevision))
                        ProgressPercent = p;
                });
                // Пустая операция в снимке возможна: файл проекта, написанный
                // вручную, способен принести и такую. Отклоняет её проверка
                // перед генерацией, поэтому список передаётся как есть.
                // Траектория строится один раз: постпроцессор делает из неё
                // программу, а трёхмерный предпросмотр показывает её саму.
                // Стойку выбирает настройка; проверка внутри BuildToolPath
                // уже отказала бы на неизвестном ключе.
                var (toolPath, program) = await Task.Run(() =>
                {
                    var snapshot = payload.Deserialize();
                    var settings = snapshot.Settings;
                    var path = _generator.BuildToolPath(snapshot.Operations, settings, progress, cancellation.Token);
                    return (path, _postProcessors.For(settings.Format.PostProcessorName).Build(path, settings));
                }, cancellation.Token);

                if (generationRevision != Volatile.Read(ref _documentRevision))
                {
                    ProgressPercent = 0;
                    _logger.Info("G-code generation result discarded: operations changed while generating");
                    return;
                }

                GeneratedToolPath = toolPath;

                // Программа хранится строками, как её и построил генератор:
                // предпросмотр показывает их виртуализированным списком, а
                // единый текст собирается только при сохранении в файл.
                ProgramLines = program.Lines as IReadOnlyList<string> ?? new List<string>(program.Lines);
                generationCompleted = true;
                _logger.Info($"G-code generated: {operationCount} operation(s), {program.Lines.Count} line(s)");
            }
            catch (OperationCanceledException)
            {
                // Документ изменился, пока строилась программа: это не сбой,
                // а отказ от заведомо ненужного результата.
                ProgressPercent = 0;
                _logger.Info("G-code generation cancelled: operations changed while generating");
            }
            catch (Exception ex)
            {
                GeneratedToolPath = null;
                ProgramLines = null;
                ProgressPercent = 0;
                _logger.Error("G-code generation failed", ex);
                var message = _localizationManager?.GetString("ErrorGeneratingGCode") ?? "ErrorGeneratingGCode";
                var errorTitle = _localizationManager?.GetString("Error") ?? "Error";
                _messageService.ShowError($"{message}\n{CoreErrorMessages.Describe(ex, _localizationManager)}", errorTitle);
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
            if (ProgramLines is not { Count: > 0 })
                return;

            var fileName = _fileDialogService.ShowSaveDialog(
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
                _messageService.ShowError($"{message}\n{CoreErrorMessages.Describe(ex, _localizationManager)}", errorTitle);
            }
        }

        private void PreviewGCode()
        {
            if (ProgramLines is not { Count: > 0 })
                return;

            var viewModel = _createPreview();
            viewModel.ToolPath = GeneratedToolPath;
            _dialogHost.ShowDialog(viewModel);
        }
    }
}
