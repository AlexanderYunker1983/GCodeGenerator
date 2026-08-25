#nullable enable
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Owns the user workflow for creating, opening and saving a project.
    /// The operation collection keeps its identity so all category views stay bound.
    ///
    /// Здесь же живёт состояние документа: файл, с которым работает
    /// пользователь, и признак несохранённых изменений. Без них программа
    /// не отличала сохранённый проект от изменённого: закрытие окна молча
    /// теряло работу, а «Сохранить проект» каждый раз спрашивало имя файла
    /// заново и предлагало перезаписать только что сохранённый.
    /// </summary>
    public sealed class ProjectWorkflowViewModel : ViewModelBase
    {
        private readonly ObservableCollection<OperationBase> _operations;
        private readonly GCodeWorkflowViewModel _gCodeWorkflow;
        private readonly ILocalizationManager? _localizationManager;
        private readonly IMessageService _messageService;
        private readonly IFileDialogService _fileDialogService;
        private readonly ISettingsStore? _settingsStore;
        private readonly IProjectFileService _projectFileService;
        private readonly IAppLogger _logger;

        /// <summary>
        /// Документ меняется самой программой (создание, загрузка, сброс),
        /// а не пользователем: такие изменения не делают проект несохранённым.
        /// </summary>
        private bool _isApplyingDocument;

        private string? _currentPath;
        private bool _isDirty;

        internal ProjectWorkflowViewModel(
            ObservableCollection<OperationBase> operations,
            GCodeWorkflowViewModel gCodeWorkflow,
            ILocalizationManager? localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            ISettingsStore? settingsStore,
            IProjectFileService projectFileService,
            IAppLogger? logger = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _gCodeWorkflow = gCodeWorkflow ?? throw new ArgumentNullException(nameof(gCodeWorkflow));
            _localizationManager = localizationManager;
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));
            _logger = logger ?? NullAppLogger.Instance;

            NewProgramCommand = new RelayCommand(CreateNewProgram);
            SaveProjectCommand = new RelayCommand(SaveProject, () => _operations.Count > 0);
            SaveProjectAsCommand = new RelayCommand(SaveProjectAs, () => _operations.Count > 0);
            OpenProjectCommand = new RelayCommand(OpenProject);
        }

        public event EventHandler? ProjectResetting;

        /// <summary>Документ заменяется целиком: началась загрузка или сброс.</summary>
        public event EventHandler? DocumentApplying;

        /// <summary>Замена документа завершена.</summary>
        public event EventHandler? DocumentApplied;

        public ICommand NewProgramCommand { get; }

        /// <summary>Сохраняет в текущий файл; имя спрашивается только у нового проекта.</summary>
        public ICommand SaveProjectCommand { get; }

        /// <summary>Сохраняет в другой файл — имя спрашивается всегда.</summary>
        public ICommand SaveProjectAsCommand { get; }

        public ICommand OpenProjectCommand { get; }

        /// <summary>Файл текущего проекта или <c>null</c>, если он ещё не сохранялся.</summary>
        public string? CurrentPath
        {
            get => _currentPath;
            private set
            {
                if (Equals(value, _currentPath)) return;
                _currentPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentFileName));
            }
        }

        /// <summary>
        /// Имя файла проекта без пути; пусто, если проект ещё не сохранялся.
        /// Выделяется строковой операцией, а не средствами файловой системы:
        /// view-модели с ней не работают — это забота служб.
        /// </summary>
        public string CurrentFileName
        {
            get
            {
                var path = CurrentPath;
                if (string.IsNullOrEmpty(path))
                    return string.Empty;

                var separator = path.LastIndexOfAny(new[] { '\\', '/' });
                return separator >= 0 ? path.Substring(separator + 1) : path;
            }
        }

        /// <summary>В проекте есть изменения, которых нет в файле.</summary>
        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (value == _isDirty) return;
                _isDirty = value;
                OnPropertyChanged();
            }
        }

        public void NotifyOperationsChanged()
        {
            ((RelayCommand)SaveProjectCommand).NotifyCanExecuteChanged();
            ((RelayCommand)SaveProjectAsCommand).NotifyCanExecuteChanged();
            MarkDirty();
        }

        /// <summary>
        /// Отмечает документ изменённым. Изменения, которые вносит сама
        /// программа (загрузка проекта, создание нового), не считаются.
        /// </summary>
        public void MarkDirty()
        {
            if (_isApplyingDocument)
                return;
            IsDirty = true;
        }

        /// <summary>
        /// Спрашивает о несохранённых изменениях перед действием, которое их
        /// потеряет: созданием нового проекта, открытием другого и закрытием
        /// программы.
        /// </summary>
        /// <returns><c>false</c> — пользователь передумал, действие выполнять нельзя.</returns>
        public bool ConfirmDiscardChanges()
        {
            if (!IsDirty)
                return true;

            var answer = _messageService.ShowSaveConfirmation(
                Localize("ConfirmSaveChangesMessage"),
                Localize("ConfirmSaveChangesTitle"));

            switch (answer)
            {
                case SaveConfirmation.Save:
                    // Сохранение может не состояться: пользователь закрыл
                    // диалог выбора файла или запись не удалась — тогда
                    // исходное действие тоже отменяется.
                    return SaveToFile(CurrentPath ?? AskFileName());
                case SaveConfirmation.Discard:
                    return true;
                default:
                    return false;
            }
        }

        private void CreateNewProgram()
        {
            if (!ConfirmDiscardChanges())
                return;

            ApplyDocument(() =>
            {
                ResetOperations();
                _settingsStore?.RestoreGlobalGenerationSettings();
                CurrentPath = null;
            });
        }

        private void SaveProject()
        {
            if (_operations.Count == 0)
                return;

            SaveToFile(CurrentPath ?? AskFileName());
        }

        private void SaveProjectAs()
        {
            if (_operations.Count == 0)
                return;

            SaveToFile(AskFileName());
        }

        /// <summary>Спрашивает имя файла проекта; <c>null</c> — пользователь отменил.</summary>
        private string? AskFileName()
        {
            var filter = Localize("ProjectFileFilter");
            var title = Localize("SaveProjectTitle");
            var suggested = CurrentFileName is { Length: > 0 } current ? current : "project.ygc";
            return _fileDialogService.ShowSaveDialog(title, filter, "ygc", suggested);
        }

        /// <summary>
        /// Сохраняет проект в файл и запоминает его как текущий.
        /// </summary>
        /// <returns><c>false</c>, если сохранение не состоялось.</returns>
        /// <param name="fileName">
        /// Имя файла; пусто, если пользователь закрыл диалог выбора — тогда
        /// сохранение не состоялось.
        /// </param>
        private bool SaveToFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            try
            {
                _projectFileService.Save(fileName, _operations, _settingsStore?.Current);
                CurrentPath = fileName;
                IsDirty = false;
                _logger.Info($"Project saved: {fileName} ({_operations.Count} operation(s))");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Saving project failed: {fileName}", ex);
                var message = Localize("ErrorSavingProject");
                _messageService.ShowError($"{message}\n{ex.Message}", Localize("SaveProjectTitle"));
                return false;
            }
        }

        private void OpenProject()
        {
            if (!ConfirmDiscardChanges())
                return;

            var filter = Localize("ProjectFileFilter");
            var title = Localize("OpenProjectTitle");
            var fileName = _fileDialogService.ShowOpenDialog(title, filter, "ygc");
            if (fileName == null)
                return;

            try
            {
                // Файл разбирается целиком до того, как меняется состояние
                // программы: непригодный проект отвергается, а открытый
                // остаётся нетронутым.
                var data = _projectFileService.Load(fileName);
                if (data?.Operations == null)
                {
                    _logger.Warning($"Project file has no operations section: {fileName}");
                    _messageService.ShowError(Localize("InvalidProjectFile"), title);
                    return;
                }

                ApplyDocument(() =>
                {
                    ApplyProjectSettings(data);
                    ResetOperations();
                    foreach (var operation in data.Operations)
                        _operations.Add(operation);
                    CurrentPath = fileName;
                });
                _logger.Info($"Project opened: {fileName} ({data.Operations.Count} operation(s))");
            }
            catch (Exception ex)
            {
                _logger.Error($"Opening project failed: {fileName}", ex);
                var message = Localize("ErrorOpeningProject");
                _messageService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        /// <summary>
        /// Выполняет замену документа: изменения, которые она вызывает, не
        /// делают проект несохранённым, а по завершении признак сбрасывается.
        /// </summary>
        private void ApplyDocument(Action apply)
        {
            _isApplyingDocument = true;
            DocumentApplying?.Invoke(this, EventArgs.Empty);
            try
            {
                apply();
            }
            finally
            {
                // Сначала закрывается пакет изменений: отложенные уведомления
                // о новом содержимом должны прийти, пока замена документа
                // ещё считается своей, иначе они пометят проект изменённым.
                DocumentApplied?.Invoke(this, EventArgs.Empty);
                _isApplyingDocument = false;
                IsDirty = false;
            }
        }

        private void ResetOperations()
        {
            ProjectResetting?.Invoke(this, EventArgs.Empty);
            _gCodeWorkflow.InvalidateGeneratedProgram();
            _operations.Clear();
        }

        private void ApplyProjectSettings(ProjectFileData data)
        {
            _settingsStore?.RestoreGlobalGenerationSettings();
            var settings = _settingsStore?.Current;
            if (settings == null)
                return;
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
