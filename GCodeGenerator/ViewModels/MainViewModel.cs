using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.PocketMill;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GCodeGenerator.GCodeGenerators;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly IGCodeGenerator _generator;
        private readonly GCodeSettings _settings = Models.GCodeSettingsStore.Current;
        private readonly ILocalizationManager _localizationManager;

        public event Action OperationsChanged;
        public event Action ShowAllRequested;

        public MainViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            _generator = new SimpleGCodeGenerator();

            DrillOperations = new DrillOperationsViewModel(localizationManager);
            DrillOperations.MainViewModel = this;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(localizationManager);
            ProfileMillingOperations.MainViewModel = this;
            
            AllOperations = new ObservableCollection<OperationBase>();
            
            // Subscribe to collection changes BEFORE initializing
            DrillOperations.Operations.CollectionChanged += OnOperationsCollectionChanged;
            ProfileMillingOperations.Operations.CollectionChanged += OnOperationsCollectionChanged;
            
            // Subscribe to AllOperations changes to update command
            AllOperations.CollectionChanged += (s, e) => ((RelayCommand)GenerateGCodeCommand)?.RaiseCanExecuteChanged();
            
            // Initialize AllOperations with existing operations
            foreach (var op in DrillOperations.Operations)
                AllOperations.Add(op);
            foreach (var op in ProfileMillingOperations.Operations)
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

            var title = _localizationManager?.GetString("MainTitle");
            var baseTitle = string.IsNullOrEmpty(title) ? "Генератор G-кода" : title;
            var version = PlatformVariables.ProgramVersion;
            _displayName = string.IsNullOrEmpty(version) ? baseTitle : $"{baseTitle} v.{version}";
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
                }
                else
                {
                    DrillOperations.SelectedOperation = null;
                    ProfileMillingOperations.SelectedOperation = null;
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
                ((RelayCommand)SaveGCodeCommand)?.RaiseCanExecuteChanged();
                ((RelayCommand)PreviewGCodeCommand)?.RaiseCanExecuteChanged();
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
                    AllOperations.Remove(item);
                    if (SelectedOperation == item)
                        SelectedOperation = null;
                }
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Remove all items from this collection
                var toRemove = AllOperations.Where(op => 
                    (sender == DrillOperations.Operations && DrillOperations.Operations.Contains(op)) ||
                    (sender == ProfileMillingOperations.Operations && ProfileMillingOperations.Operations.Contains(op))
                ).ToList();
                foreach (var item in toRemove)
                {
                    AllOperations.Remove(item);
                    if (SelectedOperation == item)
                        SelectedOperation = null;
                }
            }
            
            // Update command state after collection changes
            ((RelayCommand)GenerateGCodeCommand)?.RaiseCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
        }

        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(AllOperations), _settings);
            var sb = new StringBuilder();
            foreach (var line in program.Lines)
                sb.AppendLine(line);
            GCodePreview = sb.ToString();
            ((RelayCommand)SaveGCodeCommand).RaiseCanExecuteChanged();
        }

        private void SaveGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "G-code files (*.nc)|*.nc|All files (*.*)|*.*",
                DefaultExt = "nc",
                FileName = "program.nc"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, GCodePreview, System.Text.Encoding.UTF8);
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Ошибка при сохранении файла:\n{ex.Message}",
                        "Ошибка",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void PreviewGCode()
        {
            if (string.IsNullOrEmpty(GCodePreview))
                return;

            using (var vm = GetViewModel<PreviewViewModel>())
            {
                vm.GCodeText = GCodePreview;
                vm.ShowAsync();
            }
        }

        private void OpenSettings()
        {
            using (var vm = GetViewModel<SettingsViewModel>())
            {
                vm.ShowAsync();
            }
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
            var index = AllOperations.IndexOf(SelectedOperation);
            if (index <= 0) return false;
            
            // Check if operation can be moved in its own collection
            if (DrillOperations.Operations.Contains(SelectedOperation))
            {
                var drillIndex = DrillOperations.Operations.IndexOf(SelectedOperation);
                return drillIndex > 0;
            }
            else if (ProfileMillingOperations.Operations.Contains(SelectedOperation))
            {
                var profileIndex = ProfileMillingOperations.Operations.IndexOf(SelectedOperation);
                return profileIndex > 0;
            }
            return false;
        }

        private bool CanMoveSelectedOperationDown()
        {
            if (SelectedOperation == null) return false;
            var index = AllOperations.IndexOf(SelectedOperation);
            if (index < 0 || index >= AllOperations.Count - 1) return false;
            
            // Check if operation can be moved in its own collection
            if (DrillOperations.Operations.Contains(SelectedOperation))
            {
                var drillIndex = DrillOperations.Operations.IndexOf(SelectedOperation);
                return drillIndex >= 0 && drillIndex < DrillOperations.Operations.Count - 1;
            }
            else if (ProfileMillingOperations.Operations.Contains(SelectedOperation))
            {
                var profileIndex = ProfileMillingOperations.Operations.IndexOf(SelectedOperation);
                return profileIndex >= 0 && profileIndex < ProfileMillingOperations.Operations.Count - 1;
            }
            return false;
        }

        private void MoveSelectedOperationUp()
        {
            if (!CanMoveSelectedOperationUp()) return;
            
            if (DrillOperations.Operations.Contains(SelectedOperation))
            {
                DrillOperations.MoveSelectedOperationUp();
            }
            else if (ProfileMillingOperations.Operations.Contains(SelectedOperation))
            {
                ProfileMillingOperations.MoveSelectedOperationUp();
            }
            
            // Update AllOperations order
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
            
            if (DrillOperations.Operations.Contains(SelectedOperation))
            {
                DrillOperations.MoveSelectedOperationDown();
            }
            else if (ProfileMillingOperations.Operations.Contains(SelectedOperation))
            {
                ProfileMillingOperations.MoveSelectedOperationDown();
            }
            
            // Update AllOperations order
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
            
            var operationToRemove = SelectedOperation;
            
            if (DrillOperations.Operations.Contains(operationToRemove))
            {
                DrillOperations.RemoveSelectedOperation();
            }
            else if (ProfileMillingOperations.Operations.Contains(operationToRemove))
            {
                ProfileMillingOperations.RemoveSelectedOperation();
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
        }

        private void UpdateOperationCommandsCanExecute()
        {
            ((RelayCommand)MoveOperationUpCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)MoveOperationDownCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)RemoveOperationCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)EditOperationCommand)?.RaiseCanExecuteChanged();
        }

    }
}