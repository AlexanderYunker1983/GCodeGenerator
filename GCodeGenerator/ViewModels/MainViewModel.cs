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
using System.Text;
using System.Windows.Input;
using System.ComponentModel;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly IGCodeGenerator _generator;
        private readonly GCodeSettings _settings;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly IProgramInfo _programInfo;
        private readonly IThemeService _themeService;
        private readonly ProjectFileService _projectFileService = new ProjectFileService();

        public event Action OperationsChanged;
        public event Action ShowAllRequested;

        public MainViewModel(ILocalizationManager localizationManager, IDialogService dialogService, IGCodeGenerator generator, IOperationEditorFactory operationEditorFactory, IProgramInfo programInfo, ISettingsStore settingsStore, IThemeService themeService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            // Пункт 4.5 плана: генератор резолвится через IoC (App.xaml.cs),
            // new SimpleGCodeGenerator() удалён.
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            // Пункт 7.3 плана: фабрика диалогов редактора операций.
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            // Пункт 7.5 плана: версия, настройки и тема — через IoC (ранее статика
            // PlatformVariables/GCodeSettingsStore/ThemeHelper).
            _programInfo = programInfo ?? throw new ArgumentNullException(nameof(programInfo));
            _settings = (settingsStore ?? throw new ArgumentNullException(nameof(settingsStore))).Current;
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

            // Пункт 7.2 плана: AllOperations — единый источник истины по операциям;
            // категориальные VM получают его и открывают фильтрованные представления
            // (FilteredOperationsView).
            AllOperations = new ObservableCollection<OperationBase>();
            AllOperations.CollectionChanged += OnAllOperationsCollectionChanged;

            // Пункт 7.3 плана: категорийные VM открывают диалоги через фабрику.
            DrillOperations = new DrillOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            DrillOperations.OperationAdded += OnCategoryOperationAdded;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            ProfileMillingOperations.OperationAdded += OnCategoryOperationAdded;
            PocketOperations = new Pocket.PocketOperationsViewModel(localizationManager, operationEditorFactory, AllOperations);
            PocketOperations.OperationAdded += OnCategoryOperationAdded;
            
            GenerateGCodeCommand = new RelayCommand(GenerateGCode, () => AllOperations.Count > 0);
            SaveGCodeCommand = new RelayCommand(SaveGCode, () => !string.IsNullOrEmpty(GCodePreview));
            PreviewGCodeCommand = new RelayCommand(PreviewGCode, () => !string.IsNullOrEmpty(GCodePreview));
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            ShowAllPreviewCommand = new RelayCommand(ShowAllPreview);
            
            MoveOperationUpCommand = new RelayCommand(MoveSelectedOperationUp, CanMoveSelectedOperationUp);
            MoveOperationDownCommand = new RelayCommand(MoveSelectedOperationDown, CanMoveSelectedOperationDown);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, CanModifySelectedOperation);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, CanModifySelectedOperation);
            NewProgramCommand = new RelayCommand(CreateNewProgram);
            SaveProjectCommand = new RelayCommand(SaveProject, CanSaveProject);
            OpenProjectCommand = new RelayCommand(OpenProject);

            var title = _localizationManager?.GetString("MainTitle");
            var baseTitle = string.IsNullOrEmpty(title) ? "Генератор G-кода" : title;
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
            OperationsPreview = new OperationsPreviewViewModel(this, _themeService);
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
            }
        }

        private string _gCodePreview;

        public string GCodePreview
        {
            get => _gCodePreview;
            set
            {
                if (Equals(value, _gCodePreview)) return;
                _gCodePreview = value;
                OnPropertyChanged();
                ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
                ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            }
        }

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


        private void OnAllOperationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ((RelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();

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
            NotifyOperationsChanged();
        }

        private GCodeProgram _generatedProgram;

        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(AllOperations), _settings);
            _generatedProgram = program;
            var sb = new StringBuilder();
            foreach (var line in program.Lines)
                sb.AppendLine(line);
            GCodePreview = sb.ToString();
            ((RelayCommand)SaveGCodeCommand).NotifyCanExecuteChanged();
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
            if (fileName != null)
            {
                try
                {
                    System.IO.File.WriteAllText(fileName, GCodePreview, System.Text.Encoding.UTF8);
                }
                catch (System.Exception ex)
                {
                    _dialogService.ShowError(
                        $"Ошибка при сохранении файла:\n{ex.Message}",
                        "Ошибка");
                }
            }
        }

        private void PreviewGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            var vm = _dialogService.CreateViewModel<PreviewViewModel>();
            vm.Program = _generatedProgram;
            _dialogService.ShowDialog(vm);
        }

        private void OpenSettings()
        {
            var vm = _dialogService.CreateViewModel<SettingsViewModel>();
            _dialogService.ShowDialog(vm);
        }

        private void ShowAllPreview()
        {
            ShowAllRequested?.Invoke();
        }

        public void NotifyOperationsChanged()
        {
            OperationsChanged?.Invoke();
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

            var message = _localizationManager?.GetString("ConfirmNewProjectMessage") ??
                          "Вы уверены, что хотите создать новый проект? Все несохраненные данные будут потеряны.";
            var title = _localizationManager?.GetString("ConfirmNewProjectTitle") ?? "Подтверждение";

            if (!_dialogService.ShowConfirm(message, title))
                return;

            // Пункт 7.2 плана: единая коллекция — один Clear()
            AllOperations.Clear();
            SelectedOperation = null;
            GCodePreview = string.Empty;
            _generatedProgram = null;

            ((RelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private bool CanSaveProject() => AllOperations.Count > 0;

        private void SaveProject()
        {
            if (!CanSaveProject()) return;

            var filter = _localizationManager?.GetString("ProjectFileFilter") ?? "Project files (*.ygc)|*.ygc|All files (*.*)|*.*";
            var title = _localizationManager?.GetString("SaveProjectTitle") ?? "Сохранить проект";

            var fileName = _dialogService.ShowSaveDialog(title, filter, "ygc", "project.ygc");
            if (fileName == null)
                return;

            try
            {
                _projectFileService.Save(fileName, AllOperations);
            }
            catch (Exception ex)
            {
                var message = _localizationManager?.GetString("ErrorSavingProject") ?? "Ошибка при сохранении проекта:";
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private void OpenProject()
        {
            if (!ConfirmResetIfNeeded())
                return;

            var filter = _localizationManager?.GetString("ProjectFileFilter") ?? "Project files (*.ygc)|*.ygc|All files (*.*)|*.*";
            var title = _localizationManager?.GetString("OpenProjectTitle") ?? "Открыть проект";

            var fileName = _dialogService.ShowOpenDialog(title, filter, "ygc");
            if (fileName == null)
                return;

            try
            {
                var operations = _projectFileService.Load(fileName);
                if (operations == null)
                {
                    ShowInvalidProjectMessage(title);
                    return;
                }

                LoadOperationsFromProject(operations);
            }
            catch (Exception ex)
            {
                var message = _localizationManager?.GetString("ErrorOpeningProject") ?? "Ошибка при загрузке проекта:";
                _dialogService.ShowError($"{message}\n{ex.Message}", title);
            }
        }

        private bool ConfirmResetIfNeeded()
        {
            var hasOperations = AllOperations.Count > 0;
            var hasGCode = !string.IsNullOrWhiteSpace(GCodePreview);
            if (!hasOperations && !hasGCode)
                return true;

            var message = _localizationManager?.GetString("ConfirmNewProjectMessage") ??
                          "Вы уверены, что хотите создать новый проект? Все несохраненные данные будут потеряны.";
            var title = _localizationManager?.GetString("ConfirmNewProjectTitle") ?? "Подтверждение";

            return _dialogService.ShowConfirm(message, title);
        }

        private void LoadOperationsFromProject(List<OperationBase> operations)
        {
            // Clear current data (пункт 7.2: единая коллекция)
            AllOperations.Clear();
            SelectedOperation = null;
            GCodePreview = string.Empty;
            _generatedProgram = null;

            // Пункт 7.2: switch AddOperationToCollections не нужен — все операции
            // идут в единую коллекцию (категория хранится на самой операции).
            foreach (var operation in operations)
            {
                AllOperations.Add(operation);
            }

            ((RelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private void ShowInvalidProjectMessage(string title)
        {
            var message = _localizationManager?.GetString("InvalidProjectFile") ?? "Невозможно прочитать файл проекта.";
            _dialogService.ShowError(message, title);
        }
    }
}