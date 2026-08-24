using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.PocketMill;
using GCodeGenerator.ViewModels.Pocket;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using System.ComponentModel;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly GCodeSettings _settings;
        private readonly GCodeWorkflowViewModel _gCodeWorkflow;
        private readonly ISettingsStore _settingsStore;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly IProgramInfo _programInfo;
        private readonly IThemeService _themeService;
        private readonly IProjectFileService _projectFileService;

        public MainViewModel(ILocalizationManager localizationManager, IDialogService dialogService, IGCodeWorkflowFactory gCodeWorkflowFactory, IOperationEditorFactory operationEditorFactory, IProgramInfo programInfo, ISettingsStore settingsStore, IThemeService themeService, IProjectFileService projectFileService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            // Пункт 7.3 плана: фабрика диалогов редактора операций.
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            // Пункт 7.5 плана: версия, настройки и тема — через IoC (ранее статика
            // PlatformVariables/GCodeSettingsStore/ThemeHelper).
            _programInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
            _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
            _settings = _settingsStore.Current;
            _settingsStore.SettingsChanged += OnSettingsChanged;
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            // Пункт 7.6 плана: служба файлов проекта через IoC (new удалён).
            _projectFileService = projectFileService ?? throw new ArgumentNullException(nameof(projectFileService));

            // Пункт 7.2 плана: AllOperations — единый источник истины по операциям;
            // категориальные VM получают его и открывают фильтрованные представления
            // (FilteredOperationsView).
            AllOperations = new ObservableCollection<OperationBase>();
            AllOperations.CollectionChanged += OnAllOperationsCollectionChanged;

            _gCodeWorkflow = (gCodeWorkflowFactory ?? throw new ArgumentNullException(nameof(gCodeWorkflowFactory)))
                .Create(AllOperations, _settings);
            _gCodeWorkflow.PropertyChanged += OnGCodeWorkflowPropertyChanged;

            // Пункт 7.3 плана: категорийные VM открывают диалоги через фабрику.
            DrillOperations = new DrillOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            DrillOperations.OperationAdded += OnCategoryOperationAdded;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            ProfileMillingOperations.OperationAdded += OnCategoryOperationAdded;
            PocketOperations = new Pocket.PocketOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            PocketOperations.OperationAdded += OnCategoryOperationAdded;
            
            // Генерация, прогресс, preview и сохранение G-code принадлежат
            // отдельному workflow; MainViewModel только публикует его команды.
            GenerateGCodeCommand = _gCodeWorkflow.GenerateGCodeCommand;
            SaveGCodeCommand = _gCodeWorkflow.SaveGCodeCommand;
            PreviewGCodeCommand = _gCodeWorkflow.PreviewGCodeCommand;
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            ShowAllPreviewCommand = new RelayCommand(ShowAllPreview);
            
            MoveOperationUpCommand = new RelayCommand(MoveSelectedOperationUp, CanMoveSelectedOperationUp);
            MoveOperationDownCommand = new RelayCommand(MoveSelectedOperationDown, CanMoveSelectedOperationDown);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, CanModifySelectedOperation);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, CanModifySelectedOperation);
            NewProgramCommand = new RelayCommand(CreateNewProgram);
            SaveProjectCommand = new RelayCommand(SaveProject, CanSaveProject);
            OpenProjectCommand = new RelayCommand(OpenProject);

            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            var baseTitle = _localizationManager?.GetString("MainTitle") ?? "MainTitle";
            var version = _programInfo.Version;
            _displayName = string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";

            // Attach property change handlers to existing operations
            foreach (var op in AllOperations)
            {
                AttachOperation(op);
            }

            // Пункт 6.3 плана: 2D-превью получает чистую OperationScene из
            // отдельного VM (code-behind — только отрисовка и мышь).
            // Пункт 7.5 плана: VM получает IThemeService (ранее code-behind
            // подписывался на статический ThemeHelper.ThemeChanged).
            // DoD фазы 7: без циклической ссылки — VM не хранит MainViewModel;
            // MainViewModel пушит сцену/выбор и подписывается на события VM.
            OperationsPreview = new OperationsPreviewViewModel(AllOperations, _themeService);
            OperationsPreview.SelectionChanged += OnPreviewSelectionChanged;
            OperationsPreview.EditRequested += OnPreviewEditRequested;
        }

        private void OnPreviewSelectionChanged(object sender, OperationBase operation)
        {
            if (Equals(operation, _selectedOperation)) return;
            SelectedOperation = operation;
        }

        private void OnPreviewEditRequested(object sender, EventArgs e)
        {
            if (CanModifySelectedOperation())
                EditSelectedOperation();
        }

        private string _displayName;

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

        public DrillOperationsViewModel DrillOperations { get; }
        
        public ProfileMillingOperationsViewModel ProfileMillingOperations { get; }

        public Pocket.PocketOperationsViewModel PocketOperations { get; }

        /// <summary>
        /// Пункт 6.3 плана: VM 2D-превью операций (чистая OperationScene).
        /// </summary>
        public OperationsPreviewViewModel OperationsPreview { get; }
        
        public ObservableCollection<OperationBase> AllOperations { get; }
        
        private OperationBase _selectedOperation;
        
        public OperationBase SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (Equals(value, _selectedOperation)) return;
                _selectedOperation = value;
                OnPropertyChanged();
                UpdateOperationCommandsCanExecute();
                NotifyOperationsChanged();
                // DoD фазы 7: выбор пушится в 2D-превью VM (без циклической ссылки).
                OperationsPreview?.SelectedOperation = value;
            }
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

        public ICommand OpenProjectCommand { get; }

        private void OnGCodeWorkflowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GCodeWorkflowViewModel.GCodePreview) ||
                e.PropertyName == nameof(GCodeWorkflowViewModel.IsGenerating) ||
                e.PropertyName == nameof(GCodeWorkflowViewModel.ProgressPercent))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }


        private void OnAllOperationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateGeneratedProgram();

            if (e?.NewItems != null)
            {
                foreach (OperationBase op in e.NewItems)
                    AttachOperation(op);
            }

            if (e?.OldItems != null)
            {
                foreach (OperationBase op in e.OldItems)
                {
                    DetachOperation(op);
                    if (SelectedOperation == op)
                        SelectedOperation = null;
                }
            }

            // Пункт 7.2 плана: категорийных коллекций больше нет — список,
            // команды и 2D-превью обновляются от единой коллекции.
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private void AttachOperation(OperationBase op)
        {
            if (op == null) return;
            op.PropertyChanged -= OnOperationPropertyChanged;
            op.PropertyChanged += OnOperationPropertyChanged;
        }

        private void DetachOperation(OperationBase op)
        {
            if (op == null) return;
            op.PropertyChanged -= OnOperationPropertyChanged;
        }

        private void OnOperationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Пункт 7.2 плана: любое изменение операции (сохранение из диалога,
            // переключение «Включено», импорт DXF) перерисовывает 2D-превью.
            InvalidateGeneratedProgram();
            NotifyOperationsChanged();
        }

        private void OpenSettings()
        {
            var vm = _dialogService.CreateViewModel<SettingsViewModel>();
            _dialogService.ShowDialog(vm);
        }

        private void ShowAllPreview()
        {
            OperationsPreview?.RaiseShowAll();
        }

        public void NotifyOperationsChanged()
        {
            // Пункт 7.2 плана: любое изменение операций перерисовывает 2D-превью
            // (push в preview VM, DoD фазы 7 — без циклической ссылки).
            OperationsPreview?.RebuildScene();
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            InvalidateGeneratedProgram();
        }

        private void InvalidateGeneratedProgram()
        {
            _gCodeWorkflow.InvalidateGeneratedProgram();
        }

        private void OnCategoryOperationAdded(OperationBase operation)
        {
            // Пользователь добавил операцию через категорийную вкладку —
            // выбираем её в общем списке (поведение как до пункта 7.2).
            SelectedOperation = operation;
        }
        
        private bool CanModifySelectedOperation() => SelectedOperation != null;

        private bool CanMoveSelectedOperationUp()
        {
            if (SelectedOperation == null) return false;
            if (AllOperations.Count < 2) return false;
            var index = AllOperations.IndexOf(SelectedOperation);
            return index > 0;
        }

        private bool CanMoveSelectedOperationDown()
        {
            if (SelectedOperation == null) return false;
            var index = AllOperations.IndexOf(SelectedOperation);
            return index >= 0 && index < AllOperations.Count - 1;
        }

        private void MoveSelectedOperationUp()
        {
            if (!CanMoveSelectedOperationUp()) return;

            var allIndex = AllOperations.IndexOf(SelectedOperation);
            if (allIndex > 0)
            {
                AllOperations.Move(allIndex, allIndex - 1);
            }

            UpdateOperationCommandsCanExecute();
        }

        private void MoveSelectedOperationDown()
        {
            if (!CanMoveSelectedOperationDown()) return;

            var allIndex = AllOperations.IndexOf(SelectedOperation);
            if (allIndex >= 0 && allIndex < AllOperations.Count - 1)
            {
                AllOperations.Move(allIndex, allIndex + 1);
            }

            UpdateOperationCommandsCanExecute();
        }

        private void RemoveSelectedOperation()
        {
            if (!CanModifySelectedOperation()) return;

            // Пункт 7.2 плана: единая коллекция — прямое удаление; выбор и
            // команды обновляются в OnAllOperationsCollectionChanged.
            AllOperations.Remove(SelectedOperation);
        }

        private void EditSelectedOperation()
        {
            var op = SelectedOperation;
            if (op == null) return;

            // Пункт 7.3 плана: диспетчеризация диалогов (реестр тип операции →
            // VM диалога, сверление по DrillMode) — в IOperationEditorFactory.
            _operationEditorFactory.ShowEditor(op, AllOperations);
        }

        private void UpdateOperationCommandsCanExecute()
        {
            ((RelayCommand)MoveOperationUpCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)MoveOperationDownCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)RemoveOperationCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)EditOperationCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveProjectCommand)?.NotifyCanExecuteChanged();
        }

        private void CreateNewProgram()
        {
            var hasOperations = AllOperations.Count > 0;
            var hasGCode = !string.IsNullOrWhiteSpace(GCodePreview);
            if (!hasOperations && !hasGCode)
                return;

            var message = _localizationManager?.GetString("ConfirmNewProjectMessage") ?? "ConfirmNewProjectMessage";
            var title = _localizationManager?.GetString("ConfirmNewProjectTitle") ?? "ConfirmNewProjectTitle";

            if (!_dialogService.ShowConfirm(message, title))
                return;

            // Пункт 7.2 плана: единая коллекция — один Clear()
            AllOperations.Clear();
            SelectedOperation = null;

            // Новый проект стартует со всеми глобальными настройками генерации,
            // не наследуя их от ранее открытого проекта. Тема UI не меняется.
            _settingsStore.RestoreGlobalGenerationSettings();

            ((IRelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private bool CanSaveProject() => AllOperations.Count > 0;

        private void SaveProject()
        {
            if (!CanSaveProject()) return;

            var filter = _localizationManager?.GetString("ProjectFileFilter") ?? "ProjectFileFilter";
            var title = _localizationManager?.GetString("SaveProjectTitle") ?? "SaveProjectTitle";

            var fileName = _dialogService.ShowSaveDialog(title, filter, "ygc", "project.ygc");
            if (fileName == null)
                return;

            try
            {
                // Все настройки, влияющие на генерацию, пишутся в файл.
                _projectFileService.Save(fileName, AllOperations, _settings);
            }
            catch (Exception ex)
            {
                var message = _localizationManager?.GetString("ErrorSavingProject") ?? "ErrorSavingProject";
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private void OpenProject()
        {
            if (!ConfirmResetIfNeeded())
                return;

            var filter = _localizationManager?.GetString("ProjectFileFilter") ?? "ProjectFileFilter";
            var title = _localizationManager?.GetString("OpenProjectTitle") ?? "OpenProjectTitle";

            var fileName = _dialogService.ShowOpenDialog(title, filter, "ygc");
            if (fileName == null)
                return;

            try
            {
                var data = _projectFileService.Load(fileName);
                if (data?.Operations == null)
                {
                    ShowInvalidProjectMessage(title);
                    return;
                }

                ApplyProjectSettings(data);
                LoadOperationsFromProject(data.Operations);
            }
            catch (Exception ex)
            {
                var message = _localizationManager?.GetString("ErrorOpeningProject") ?? "ErrorOpeningProject";
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private bool ConfirmResetIfNeeded()
        {
            var hasOperations = AllOperations.Count > 0;
            var hasGCode = !string.IsNullOrWhiteSpace(GCodePreview);
            if (!hasOperations && !hasGCode)
                return true;

            var message = _localizationManager?.GetString("ConfirmNewProjectMessage") ?? "ConfirmNewProjectMessage";
            var title = _localizationManager?.GetString("ConfirmNewProjectTitle") ?? "ConfirmNewProjectTitle";

            return _dialogService.ShowConfirm(message, title);
        }

        /// <summary>
        /// Настройки генерации проекта подставляются в сессию; отсутствующие
        /// секции старых .ygc получают глобальные значения. Тема UI не меняется.
        /// </summary>
        private void ApplyProjectSettings(ProjectFileData data)
        {
            _settingsStore.RestoreGlobalGenerationSettings();
            if (data.Format != null)
                _settings.Format = data.Format;
            if (data.Spindle != null)
                _settings.Spindle = data.Spindle;
            if (data.Coolant != null)
                _settings.Coolant = data.Coolant;
            if (data.WorkCoordinate != null)
                _settings.WorkCoordinate = data.WorkCoordinate;
        }

        private void LoadOperationsFromProject(List<OperationBase> operations)
        {
            // Clear current data (пункт 7.2: единая коллекция)
            AllOperations.Clear();
            SelectedOperation = null;

            // Пункт 7.2: switch AddOperationToCollections не нужен — все операции
            // идут в единую коллекцию (категория хранится на самой операции).
            foreach (var operation in operations)
            {
                AllOperations.Add(operation);
            }

            ((IRelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private void ShowInvalidProjectMessage(string title)
        {
            var message = _localizationManager?.GetString("InvalidProjectFile") ?? "InvalidProjectFile";
            _dialogService.ShowError(message, title);
        }
    }
}
