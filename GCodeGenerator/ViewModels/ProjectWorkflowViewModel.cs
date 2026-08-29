#nullable enable
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IDocumentRecoveryService? _recovery;
        private readonly SemaphoreSlim _workflowGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _fileIoGate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Документ меняется самой программой (создание, загрузка, сброс),
        /// а не пользователем: такие изменения не делают проект несохранённым.
        /// </summary>
        private bool _isApplyingDocument;

        /// <summary>
        /// Версия формата файла, из которого открыт текущий проект;
        /// null — проект новый. Нужна одному решению: предупредить ли при
        /// сохранении, что файл старого формата стал файлом текущей версии.
        /// </summary>
        private int? _loadedFileVersion;

        private string? _currentPath;
        private bool _isDirty;
        private long _documentRevision;

        internal ProjectWorkflowViewModel(
            ObservableCollection<OperationBase> operations,
            GCodeWorkflowViewModel gCodeWorkflow,
            ILocalizationManager? localizationManager,
            IMessageService messageService,
            IFileDialogService fileDialogService,
            ISettingsStore? settingsStore,
            IProjectFileService projectFileService,
            IAppLogger? logger = null,
            IDocumentRecoveryService? recovery = null)
        {
            _operations = operations ?? throw new ArgumentNullException(nameof(operations));
            _gCodeWorkflow = gCodeWorkflow ?? throw new ArgumentNullException(nameof(gCodeWorkflow));
            _localizationManager = localizationManager;
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));
            _logger = logger ?? NullAppLogger.Instance;
            _recovery = recovery;

            // Команды асинхронны: чтение, разбор и запись файла уходят в фон,
            // окно остаётся живым. К документу фон не прикасается — слепок
            // и применение выполняются на потоке интерфейса.
            NewProgramCommand = new AsyncRelayCommand(CreateNewProgramAsync);
            SaveProjectCommand = new AsyncRelayCommand(SaveProjectAsync);
            SaveProjectAsCommand = new AsyncRelayCommand(SaveProjectAsAsync);
            OpenProjectCommand = new AsyncRelayCommand(OpenProjectAsync);
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
            unchecked { _documentRevision++; }
            IsDirty = true;
            if (_operations.Count > 0)
                _recovery?.Schedule(SerializeProject);
            else
                _recovery?.Clear();
        }

        /// <summary>
        /// Спрашивает о несохранённых изменениях перед действием, которое их
        /// потеряет. Синхронная версия — для закрытия программы: событие
        /// закрытия окна WPF не умеет ждать, и сохранение при закрытии
        /// выполняется на месте; команды меню пользуются асинхронной.
        /// </summary>
        /// <returns><c>false</c> — пользователь передумал, действие выполнять нельзя.</returns>
        public bool ConfirmDiscardChanges()
        {
            switch (AskAboutUnsavedChanges())
            {
                case SaveConfirmation.Save:
                    // Сохранение может не состояться: пользователь закрыл
                    // диалог выбора файла или запись не удалась — тогда
                    // исходное действие тоже отменяется.
                    return SaveToFile(CurrentPath ?? AskFileName());
                case SaveConfirmation.Discard:
                    _recovery?.Clear();
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Асинхронная версия <see cref="ConfirmDiscardChanges"/> для команд
        /// создания и открытия проекта: сохранение пишет файл в фоне.
        /// </summary>
        private async Task<bool> ConfirmDiscardChangesAsync()
        {
            switch (AskAboutUnsavedChanges())
            {
                case SaveConfirmation.Save:
                    return await SaveToFileAsync(CurrentPath ?? AskFileName());
                case SaveConfirmation.Discard:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Вопрос о несохранённых изменениях; для чистого документа — сразу
        /// «отбросить»: терять нечего, спрашивать не о чем.
        /// </summary>
        private SaveConfirmation AskAboutUnsavedChanges()
        {
            if (!IsDirty)
                return SaveConfirmation.Discard;

            return _messageService.ShowSaveConfirmation(
                Localize("ConfirmSaveChangesMessage"),
                Localize("ConfirmSaveChangesTitle"));
        }

        private async Task CreateNewProgramAsync()
        {
            await _workflowGate.WaitAsync();
            try
            {
                if (!await ConfirmDiscardChangesAsync())
                    return;

                ApplyDocument(() =>
                {
                    ResetOperations();
                    _settingsStore?.RestoreGlobalGenerationSettings();
                    CurrentPath = null;
                    _loadedFileVersion = null;
                });
            }
            finally
            {
                _workflowGate.Release();
            }
        }

        private async Task SaveProjectAsync()
        {
            await _workflowGate.WaitAsync();
            try
            {
                await SaveToFileAsync(CurrentPath ?? AskFileName());
            }
            finally
            {
                _workflowGate.Release();
            }
        }

        private async Task SaveProjectAsAsync()
        {
            await _workflowGate.WaitAsync();
            try
            {
                await SaveToFileAsync(AskFileName());
            }
            finally
            {
                _workflowGate.Release();
            }
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
        /// Сохраняет проект в файл и запоминает его как текущий. Синхронная
        /// версия — для закрытия программы (см. <see cref="ConfirmDiscardChanges"/>).
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
                var savedRevision = _documentRevision;
                var operationCount = _operations.Count;
                var json = SerializeProject();
                _fileIoGate.Wait();
                try
                {
                    _projectFileService.SaveSerialized(fileName, json);
                }
                finally
                {
                    _fileIoGate.Release();
                }

                return FinishSuccessfulSave(fileName, savedRevision, operationCount);
            }
            catch (Exception ex)
            {
                return ReportSaveFailure(fileName, ex);
            }
        }

        /// <summary>
        /// Сохраняет проект, не замораживая окно: слепок снимается на потоке
        /// интерфейса — документ нельзя читать из фона, пока его может
        /// править пользователь, — а запись на диск, самую долгую часть,
        /// выполняет фоновый поток.
        /// </summary>
        /// <returns><c>false</c>, если сохранение не состоялось.</returns>
        /// <param name="fileName">Имя файла; пусто — сохранение не состоялось.</param>
        private async Task<bool> SaveToFileAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            try
            {
                var savedRevision = _documentRevision;
                var operationCount = _operations.Count;
                var json = SerializeProject();
                await Task.Run(() =>
                {
                    _fileIoGate.Wait();
                    try
                    {
                        _projectFileService.SaveSerialized(fileName, json);
                    }
                    finally
                    {
                        _fileIoGate.Release();
                    }
                });
                return FinishSuccessfulSave(fileName, savedRevision, operationCount);
            }
            catch (Exception ex)
            {
                return ReportSaveFailure(fileName, ex);
            }
        }

        /// <summary>Слепок документа для записи: снимается на потоке интерфейса.</summary>
        private string SerializeProject()
            => _projectFileService.Serialize(_operations, _settingsStore?.Current);

        /// <summary>
        /// Общее завершение удачного сохранения: файл становится текущим,
        /// признак изменений снимается — только теперь, когда данные
        /// действительно на диске.
        /// </summary>
        private bool FinishSuccessfulSave(string fileName, long savedRevision, int operationCount)
        {
            CurrentPath = fileName;
            // Пока диск писал снятый слепок, пользователь мог продолжить
            // редактирование. Такой файл сохранён успешно, но текущий
            // документ уже новее его и потому остаётся несохранённым.
            if (_documentRevision == savedRevision)
            {
                IsDirty = false;
                _recovery?.Clear();
            }
            _logger.Info($"Project saved: {fileName} ({operationCount} operation(s))");

            // Файл старого формата после сохранения стал файлом текущей
            // версии — прежние сборки программы его больше не откроют.
            // Пользователь узнаёт об этом сразу, а не при попытке открыть
            // файл там, где это уже не получится; после предупреждения
            // файл уже текущий, и повторять его незачем.
            if (_loadedFileVersion is int loadedVersion
                && loadedVersion < ProjectFileService.CurrentVersion)
            {
                _messageService.ShowInfo(
                    string.Format(
                        Localize("ProjectUpgradedFromOlderVersionInfo"),
                        loadedVersion,
                        ProjectFileService.CurrentVersion),
                    Localize("SaveProjectTitle"));
            }

            _loadedFileVersion = ProjectFileService.CurrentVersion;
            return true;
        }

        /// <summary>Общее завершение неудачного сохранения: журнал и сообщение.</summary>
        private bool ReportSaveFailure(string fileName, Exception failure)
        {
            _logger.Error($"Saving project failed: {fileName}", failure);
            var message = Localize("ErrorSavingProject");
            _messageService.ShowError($"{message}\n{CoreErrorMessages.Describe(failure, _localizationManager)}", Localize("SaveProjectTitle"));
            return false;
        }

        private async Task OpenProjectAsync()
        {
            await _workflowGate.WaitAsync();
            try
            {
                if (!await ConfirmDiscardChangesAsync())
                    return;

                var filter = Localize("ProjectFileFilter");
                var fileName = _fileDialogService.ShowOpenDialog(Localize("OpenProjectTitle"), filter, "ygc");
                if (fileName == null)
                    return;

                await LoadProjectAsync(fileName);
            }
            finally
            {
                _workflowGate.Release();
            }
        }

        /// <summary>
        /// Открывает проект из указанного файла, спросив о несохранённых
        /// изменениях.
        ///
        /// Файл приходит не только из окна выбора: программу запускают с ним
        /// в командной строке — так открывается проект двойным щелчком в
        /// проводнике, — и его же перетаскивают в окно. Прежде путь к проекту
        /// умел приходить единственным способом, поэтому собственный формат
        /// открывался только изнутри программы.
        /// </summary>
        /// <param name="fileName">Путь к файлу проекта.</param>
        /// <returns><c>true</c>, если проект открыт.</returns>
        public async Task<bool> OpenProjectAsync(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            await _workflowGate.WaitAsync();
            try
            {
                if (!await ConfirmDiscardChangesAsync())
                    return false;

                return await LoadProjectAsync(fileName!);
            }
            finally
            {
                _workflowGate.Release();
            }
        }

        /// <summary>
        /// Загружает автоматический снимок как новый несохранённый проект.
        /// Путь recovery не становится CurrentPath: обычное Ctrl+S обязано
        /// спросить имя и не перезаписать единственную спасённую копию.
        /// </summary>
        public async Task<bool> OpenRecoveryAsync(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            await _workflowGate.WaitAsync();
            try
            {
                if (!await ConfirmDiscardChangesAsync())
                    return false;

                var opened = await LoadProjectAsync(fileName!, asRecovery: true);
                if (opened)
                    MarkDirty();
                return opened;
            }
            finally
            {
                _workflowGate.Release();
            }
        }

        /// <summary>
        /// Читает файл и заменяет им документ. Вопрос о несохранённых
        /// изменениях к этому моменту уже задан.
        /// </summary>
        /// <param name="fileName">Путь к файлу проекта.</param>
        /// <param name="asRecovery">
        /// Не связывать документ с recovery-файлом и не удалять этот файл
        /// после загрузки.
        /// </param>
        /// <returns><c>true</c>, если проект открыт.</returns>
        private async Task<bool> LoadProjectAsync(string fileName, bool asRecovery = false)
        {
            var title = Localize("OpenProjectTitle");

            try
            {
                // Файл читается и разбирается в фоне целиком до того, как
                // меняется состояние программы: крупный проект не замораживает
                // окно, непригодный отвергается, а открытый остаётся нетронутым.
                var data = await Task.Run(() => _projectFileService.Load(fileName));
                if (data?.Operations == null)
                {
                    _logger.Warning($"Project file has no operations section: {fileName}");
                    _messageService.ShowError(Localize("InvalidProjectFile"), title);
                    return false;
                }

                ApplyDocument(() =>
                {
                    ApplyProjectSettings(data);
                    ResetOperations();
                    foreach (var operation in data.Operations)
                        _operations.Add(operation);
                    CurrentPath = asRecovery ? null : fileName;
                    _loadedFileVersion = data.Version;
                }, clearRecovery: !asRecovery);
                _logger.Info($"Project opened: {fileName} ({data.Operations.Count} operation(s))");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Opening project failed: {fileName}", ex);
                var message = Localize("ErrorOpeningProject");
                _messageService.ShowError($"{message}\n{CoreErrorMessages.Describe(ex, _localizationManager)}", title);
                return false;
            }
        }

        /// <summary>
        /// Выполняет замену документа: изменения, которые она вызывает, не
        /// делают проект несохранённым, а признак изменений сбрасывается
        /// только при успехе. Сбой на полпути возвращает прежние операции:
        /// документ либо заменён целиком, либо остался прежним.
        /// </summary>
        private void ApplyDocument(Action apply, bool clearRecovery = true)
        {
            _isApplyingDocument = true;
            var previousOperations = new List<OperationBase>(_operations);
            var previousPath = CurrentPath;
            var previousVersion = _loadedFileVersion;
            var previousDirty = IsDirty;
            var previousSettings = _settingsStore == null
                ? null
                : GenerationSnapshot.Capture(Array.Empty<OperationBase>(), _settingsStore.Current).Settings;
            var documentApplyingStarted = false;
            var documentRestored = false;
            try
            {
                DocumentApplying?.Invoke(this, EventArgs.Empty);
                documentApplyingStarted = true;
                try
                {
                    apply();
                }
                catch
                {
                    RestoreDocument();
                    throw;
                }
                finally
                {
                    if (documentApplyingStarted)
                        DocumentApplied?.Invoke(this, EventArgs.Empty);
                }
            }
            catch
            {
                // Исключение мог выбросить и завершающий обработчик пакета.
                // В этом случае применение уже прошло, но документ всё равно
                // обязан вернуться целиком, включая настройки и путь.
                RestoreDocument();
                throw;
            }
            finally
            {
                _isApplyingDocument = false;
            }

            // Успешная замена — документ совпадает с источником: новым
            // проектом или только что открытым файлом.
            unchecked { _documentRevision++; }
            IsDirty = false;
            if (clearRecovery)
                _recovery?.Clear();

            void RestoreDocument()
            {
                if (documentRestored)
                    return;
                documentRestored = true;

                // Восстановление идёт при ещё поднятом признаке «замена
                // документа»: откат — дело программы, не пользователя.
                _operations.Clear();
                foreach (var operation in previousOperations)
                    _operations.Add(operation);

                if (previousSettings != null)
                {
                    _settingsStore?.ApplyProjectSettings(
                        previousSettings.Format,
                        previousSettings.Spindle,
                        previousSettings.Coolant,
                        previousSettings.WorkCoordinate);
                }

                CurrentPath = previousPath;
                _loadedFileVersion = previousVersion;
                IsDirty = previousDirty;
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
            // Применяет хранилище: оно ведёт слепок генерационных настроек,
            // и мутация Current в обход него оставила бы слепок устаревшим —
            // следующее сохранение настроек ложно пометило бы проект
            // несохранённым.
            _settingsStore?.ApplyProjectSettings(data.Format, data.Spindle, data.Coolant, data.WorkCoordinate);
        }

        private string Localize(string key)
            => _localizationManager?.GetString(key) ?? key;
    }
}
