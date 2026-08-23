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
        private readonly GCodeSettings _settings = Models.GCodeSettingsStore.Current;
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly ProjectFileService _projectFileService = new ProjectFileService();

        public event Action OperationsChanged;
        public event Action ShowAllRequested;

        public MainViewModel(ILocalizationManager localizationManager, IDialogService dialogService, IGCodeGenerator generator)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            // Пункт 4.5 плана: генератор резолвится через IoC (App.xaml.cs),
            // new SimpleGCodeGenerator() удалён.
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));

            // Пункт 7.2 плана: AllOperations — единый источник истины по операциям;
            // категориальные VM получают его и открывают фильтрованные представления
            // (FilteredOperationsView).
            AllOperations = new ObservableCollection<OperationBase>();
            AllOperations.CollectionChanged += OnAllOperationsCollectionChanged;

            DrillOperations = new DrillOperationsViewModel(localizationManager, dialogService, AllOperations);
            DrillOperations.OperationAdded += OnCategoryOperationAdded;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(localizationManager, dialogService, AllOperations);
            ProfileMillingOperations.OperationAdded += OnCategoryOperationAdded;
            PocketOperations = new Pocket.PocketOperationsViewModel(localizationManager, dialogService, AllOperations);
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
            var version = PlatformVariables.ProgramVersion;
            _displayName = string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";

            // Attach property change handlers to existing operations
            foreach (var op in AllOperations)
            {
                AttachOperation(op);
            }

            // Пункт 6.3 плана: 2D-превью получает чистую OperationScene из
            // отдельного VM (code-behind — только отрисовка и мышь).
            OperationsPreview = new OperationsPreviewViewModel(this);
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

            // Пункт 7.2 плана: диспетчеризация по типу операции (категорийные
            // VM больше не хранят логику редактирования); пункт 3.4: сверление —
            // по DrillMode, а не по имени.
            switch (op)
            {
                case DrillPointsOperation drill:
                    var drillType = GetDialogViewModelType(drill.DrillMode);
                    var drillVm = (IDrillDialogViewModel)_dialogService.CreateViewModel(drillType);
                    drillVm.Operations = AllOperations;
                    drillVm.Operation = drill;
                    _dialogService.ShowDialog(drillType, drillVm);
                    break;
                case PocketCircleOperation pocketCircle:
                    var pocketCircleVm = _dialogService.CreateViewModel<PocketCircleOperationViewModel>();
                    pocketCircleVm.Operations = AllOperations;
                    pocketCircleVm.Operation = pocketCircle;
                    _dialogService.ShowDialog(pocketCircleVm);
                    break;
                case PocketRectangleOperation pocketRectangle:
                    var pocketRectangleVm = _dialogService.CreateViewModel<PocketRectangleOperationViewModel>();
                    pocketRectangleVm.Operations = AllOperations;
                    pocketRectangleVm.Operation = pocketRectangle;
                    _dialogService.ShowDialog(pocketRectangleVm);
                    break;
                case PocketEllipseOperation pocketEllipse:
                    var pocketEllipseVm = _dialogService.CreateViewModel<PocketEllipseOperationViewModel>();
                    pocketEllipseVm.Operations = AllOperations;
                    pocketEllipseVm.Operation = pocketEllipse;
                    _dialogService.ShowDialog(pocketEllipseVm);
                    break;
                case PocketDxfOperation pocketDxf:
                    var pocketDxfVm = _dialogService.CreateViewModel<PocketDxfOperationViewModel>();
                    pocketDxfVm.Operations = AllOperations;
                    pocketDxfVm.Operation = pocketDxf;
                    _dialogService.ShowDialog(pocketDxfVm);
                    break;
                case ProfileCircleOperation profileCircle:
                    var profileCircleVm = _dialogService.CreateViewModel<ProfileCircleOperationViewModel>();
                    profileCircleVm.Operations = AllOperations;
                    profileCircleVm.Operation = profileCircle;
                    _dialogService.ShowDialog(profileCircleVm);
                    break;
                case ProfileRectangleOperation profileRectangle:
                    var profileRectangleVm = _dialogService.CreateViewModel<ProfileRectangleOperationViewModel>();
                    profileRectangleVm.Operations = AllOperations;
                    profileRectangleVm.Operation = profileRectangle;
                    _dialogService.ShowDialog(profileRectangleVm);
                    break;
                case ProfileRoundedRectangleOperation profileRoundedRectangle:
                    var profileRoundedRectangleVm = _dialogService.CreateViewModel<ProfileRoundedRectangleOperationViewModel>();
                    profileRoundedRectangleVm.Operations = AllOperations;
                    profileRoundedRectangleVm.Operation = profileRoundedRectangle;
                    _dialogService.ShowDialog(profileRoundedRectangleVm);
                    break;
                case ProfileEllipseOperation profileEllipse:
                    var profileEllipseVm = _dialogService.CreateViewModel<ProfileEllipseOperationViewModel>();
                    profileEllipseVm.Operations = AllOperations;
                    profileEllipseVm.Operation = profileEllipse;
                    _dialogService.ShowDialog(profileEllipseVm);
                    break;
                case ProfilePolygonOperation profilePolygon:
                    var profilePolygonVm = _dialogService.CreateViewModel<ProfilePolygonOperationViewModel>();
                    profilePolygonVm.Operations = AllOperations;
                    profilePolygonVm.Operation = profilePolygon;
                    _dialogService.ShowDialog(profilePolygonVm);
                    break;
                case ProfileDxfOperation profileDxf:
                    var profileDxfVm = _dialogService.CreateViewModel<ProfileDxfOperationViewModel>();
                    profileDxfVm.Operations = AllOperations;
                    profileDxfVm.Operation = profileDxf;
                    _dialogService.ShowDialog(profileDxfVm);
                    break;
            }
        }

        /// <summary>
        /// Тип диалоговой view-модели для режима сверления (пункт 3.4 плана):
        /// диспетчеризация по <see cref="DrillMode"/>, а не по имени операции.
        /// Пункт 7.2: перенесён из DrillOperationsViewModel.
        /// </summary>
        internal Type GetDialogViewModelType(DrillMode mode)
        {
            switch (mode)
            {
                case DrillMode.Line: return typeof(DrillLineOperationViewModel);
                case DrillMode.Array: return typeof(DrillArrayOperationViewModel);
                case DrillMode.Rect: return typeof(DrillRectOperationViewModel);
                case DrillMode.Circle: return typeof(DrillCircleOperationViewModel);
                case DrillMode.Arc: return typeof(DrillArcOperationViewModel);
                case DrillMode.Polygon: return typeof(DrillPolygonOperationViewModel);
                case DrillMode.Ellipse: return typeof(DrillEllipseOperationViewModel);
                case DrillMode.Package: return typeof(DrillPackageOperationViewModel);
                default: return typeof(DrillPointsOperationViewModel);
            }
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