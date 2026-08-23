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

            DrillOperations = new DrillOperationsViewModel(localizationManager, dialogService);
            DrillOperations.MainViewModel = this;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(localizationManager, dialogService);
            ProfileMillingOperations.MainViewModel = this;
            PocketOperations = new Pocket.PocketOperationsViewModel(localizationManager, dialogService);
            PocketOperations.MainViewModel = this;
            
            AllOperations = new ObservableCollection<OperationBase>();
            
            // Subscribe to collection changes BEFORE initializing
            DrillOperations.Operations.CollectionChanged += OnOperationsCollectionChanged;
            ProfileMillingOperations.Operations.CollectionChanged += OnOperationsCollectionChanged;
            PocketOperations.Operations.CollectionChanged += OnOperationsCollectionChanged;
            
            // Subscribe to AllOperations changes to update command
            AllOperations.CollectionChanged += OnAllOperationsCollectionChanged;
            
            // Initialize AllOperations with existing operations
            foreach (var op in DrillOperations.Operations)
                AllOperations.Add(op);
            foreach (var op in ProfileMillingOperations.Operations)
                AllOperations.Add(op);
            foreach (var op in PocketOperations.Operations)
                AllOperations.Add(op);
            
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
                
                // Update selected operation in corresponding ViewModel
                if (value != null)
                {
                    if (DrillOperations.Operations.Contains(value))
                        DrillOperations.SelectedOperation = value;
                    else if (ProfileMillingOperations.Operations.Contains(value))
                        ProfileMillingOperations.SelectedOperation = value;
                    else if (PocketOperations.Operations.Contains(value))
                        PocketOperations.SelectedOperation = value;
                }
                else
                {
                    DrillOperations.SelectedOperation = null;
                    ProfileMillingOperations.SelectedOperation = null;
                    PocketOperations.SelectedOperation = null;
                }
                
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


        private void OnOperationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Sync AllOperations collection
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (OperationBase item in e.NewItems)
                {
                    if (!AllOperations.Contains(item))
                        AllOperations.Add(item);
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (OperationBase item in e.OldItems)
                {
                    DetachOperation(item);
                    AllOperations.Remove(item);
                    if (SelectedOperation == item)
                        SelectedOperation = null;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (OperationBase item in e.OldItems)
                {
                    DetachOperation(item);
                    AllOperations.Remove(item);
                }
                foreach (OperationBase item in e.NewItems)
                {
                    if (!AllOperations.Contains(item))
                    {
                        AllOperations.Add(item);
                        AttachOperation(item);
                    }
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Remove all items from this collection
                var toRemove = AllOperations.Where(op => 
                    (sender == DrillOperations.Operations && DrillOperations.Operations.Contains(op)) ||
                    (sender == ProfileMillingOperations.Operations && ProfileMillingOperations.Operations.Contains(op)) ||
                    (sender == PocketOperations.Operations && PocketOperations.Operations.Contains(op))
                ).ToList();
                foreach (var item in toRemove)
                {
                    DetachOperation(item);
                    AllOperations.Remove(item);
                    if (SelectedOperation == item)
                        SelectedOperation = null;
                }
            }
            
            // Update command state after collection changes
            ((RelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

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
                    DetachOperation(op);
            }
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
            if (e.PropertyName == nameof(OperationBase.IsEnabled))
            {
                // When user toggles "Enabled" flag, force 2D preview redraw
                NotifyOperationsChanged();
            }
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
                SyncOperationCollectionsOrder();
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
                SyncOperationCollectionsOrder();
            }

            UpdateOperationCommandsCanExecute();
        }

        private void RemoveSelectedOperation()
        {
            if (!CanModifySelectedOperation()) return;
            
            var operationToRemove = SelectedOperation;
            
            if (DrillOperations.Operations.Contains(operationToRemove))
            {
                DrillOperations.RemoveSelectedOperation();
            }
            else if (ProfileMillingOperations.Operations.Contains(operationToRemove))
            {
                ProfileMillingOperations.RemoveSelectedOperation();
            }
            else if (PocketOperations.Operations.Contains(operationToRemove))
            {
                PocketOperations.RemoveSelectedOperation();
            }
            
            // SelectedOperation will be updated by OnOperationsCollectionChanged
            UpdateOperationCommandsCanExecute();
        }

        private void EditSelectedOperation()
        {
            if (SelectedOperation == null) return;
            
            if (DrillOperations.Operations.Contains(SelectedOperation))
            {
                DrillOperations.EditSelectedOperation();
            }
            else if (ProfileMillingOperations.Operations.Contains(SelectedOperation))
            {
                ProfileMillingOperations.EditSelectedOperation();
            }
            else if (PocketOperations.Operations.Contains(SelectedOperation))
            {
                PocketOperations.EditSelectedOperation();
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

            // Clear all operations in specific collections first
            DrillOperations?.Operations.Clear();
            ProfileMillingOperations?.Operations.Clear();
            PocketOperations?.Operations.Clear();

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
            // Clear current data
            DrillOperations?.Operations.Clear();
            ProfileMillingOperations?.Operations.Clear();
            PocketOperations?.Operations.Clear();
            AllOperations.Clear();
            SelectedOperation = null;
            GCodePreview = string.Empty;
            _generatedProgram = null;

            foreach (var operation in operations)
            {
                AddOperationToCollections(operation);
            }

            ((RelayCommand)GenerateGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)SaveGCodeCommand)?.NotifyCanExecuteChanged();
            ((RelayCommand)PreviewGCodeCommand)?.NotifyCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private void AddOperationToCollections(OperationBase operation)
        {
            switch (operation)
            {
                case Models.DrillPointsOperation drill:
                    DrillOperations?.Operations.Add(drill);
                    break;
                case Models.ProfileRectangleOperation profileRect:
                case Models.ProfileRoundedRectangleOperation profileRounded:
                case Models.ProfileCircleOperation profileCircle:
                case Models.ProfileEllipseOperation profileEllipse:
                case Models.ProfilePolygonOperation profilePolygon:
                case Models.ProfileDxfOperation profileDxf:
                    ProfileMillingOperations?.Operations.Add(operation);
                    break;
                case Models.PocketRectangleOperation pocketRect:
                case Models.PocketCircleOperation pocketCircle:
                case Models.PocketEllipseOperation pocketEllipse:
                    PocketOperations?.Operations.Add(operation);
                    break;
                default:
                    AllOperations.Add(operation);
                    break;
            }
        }

        private void ShowInvalidProjectMessage(string title)
        {
            var message = _localizationManager?.GetString("InvalidProjectFile") ?? "Невозможно прочитать файл проекта.";
            _dialogService.ShowError(message, title);
        }

        private void SyncOperationCollectionsOrder()
        {
            SyncCollectionOrder(AllOperations, DrillOperations?.Operations);
            SyncCollectionOrder(AllOperations, ProfileMillingOperations?.Operations);
            SyncCollectionOrder(AllOperations, PocketOperations?.Operations);
        }

        private static void SyncCollectionOrder(ObservableCollection<OperationBase> sourceOrder, ObservableCollection<OperationBase> target)
        {
            if (sourceOrder == null || target == null) return;

            var desiredOrder = sourceOrder.Where(target.Contains).ToList();
            for (int desiredIndex = 0; desiredIndex < desiredOrder.Count; desiredIndex++)
            {
                var currentIndex = target.IndexOf(desiredOrder[desiredIndex]);
                if (currentIndex >= 0 && currentIndex != desiredIndex)
                {
                    target.Move(currentIndex, desiredIndex);
                }
            }
        }
    }
}