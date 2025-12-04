using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class MainViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly IGCodeGenerator _generator;
        private readonly GCodeSettings _settings = Models.GCodeSettingsStore.Current;
        private readonly ILocalizationManager _localizationManager;

        public MainViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            _generator = new SimpleGCodeGenerator();

            Operations = new ObservableCollection<OperationBase>();
            AddDrillPointsCommand = new RelayCommand(AddDrillPoints);
            AddDrillLineCommand = new RelayCommand(AddDrillLine);
            AddDrillArrayCommand = new RelayCommand(AddDrillArray);
            AddDrillRectCommand = new RelayCommand(AddDrillRect);
            AddDrillCircleCommand = new RelayCommand(AddDrillCircle);
            AddDrillPackageCommand = new RelayCommand(AddDrillPackage);
            GenerateGCodeCommand = new RelayCommand(GenerateGCode, () => Operations.Count > 0);
            SaveGCodeCommand = new RelayCommand(SaveGCode, () => !string.IsNullOrEmpty(GCodePreview));
            PreviewGCodeCommand = new RelayCommand(PreviewGCode, () => !string.IsNullOrEmpty(GCodePreview));
            OpenSettingsCommand = new RelayCommand(OpenSettings);
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

        public ObservableCollection<OperationBase> Operations { get; }

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

        public ICommand AddDrillPointsCommand { get; }

        public ICommand AddDrillLineCommand { get; }

        public ICommand AddDrillArrayCommand { get; }

        public ICommand AddDrillRectCommand { get; }

        public ICommand AddDrillCircleCommand { get; }

        public ICommand AddDrillPackageCommand { get; }

        public ICommand GenerateGCodeCommand { get; }

        public ICommand SaveGCodeCommand { get; }

        public ICommand PreviewGCodeCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand MoveOperationUpCommand { get; }

        public ICommand MoveOperationDownCommand { get; }

        public ICommand RemoveOperationCommand { get; }

        public ICommand EditOperationCommand { get; }

        private void AddDrillPoints()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("DrillPointsName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillPointsOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddDrillLine()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillLine");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillLineOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddDrillArray()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillArray");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillArrayOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddDrillRect()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillRect");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillRectOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddDrillCircle()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillCircle");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillCircleOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddDrillPackage()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillPackage");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();

            using (var vm = GetViewModel<DrillPackageOperationViewModel>())
            {
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(Operations), _settings);
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

        private bool CanModifySelectedOperation() => SelectedOperation != null;

        private bool CanMoveSelectedOperationUp()
        {
            if (SelectedOperation == null) return false;
            var index = Operations.IndexOf(SelectedOperation);
            return index > 0;
        }

        private bool CanMoveSelectedOperationDown()
        {
            if (SelectedOperation == null) return false;
            var index = Operations.IndexOf(SelectedOperation);
            return index >= 0 && index < Operations.Count - 1;
        }

        private void MoveSelectedOperationUp()
        {
            if (!CanMoveSelectedOperationUp()) return;
            var index = Operations.IndexOf(SelectedOperation);
            Operations.Move(index, index - 1);
            UpdateOperationCommandsCanExecute();
        }

        private void MoveSelectedOperationDown()
        {
            if (!CanMoveSelectedOperationDown()) return;
            var index = Operations.IndexOf(SelectedOperation);
            Operations.Move(index, index + 1);
            UpdateOperationCommandsCanExecute();
        }

        private void RemoveSelectedOperation()
        {
            if (!CanModifySelectedOperation()) return;
            var index = Operations.IndexOf(SelectedOperation);
            if (index < 0) return;
            Operations.RemoveAt(index);
            SelectedOperation = index < Operations.Count ? Operations[index] : null;
            ((RelayCommand)GenerateGCodeCommand).RaiseCanExecuteChanged();
            UpdateOperationCommandsCanExecute();
        }

        private void EditSelectedOperation()
        {
            if (!(SelectedOperation is DrillPointsOperation drillOp))
                return;

            // Определяем тип операции по имени
            var operationName = drillOp.Name;
            var addDrillLineName = _localizationManager?.GetString("AddDrillLine");
            var addDrillArrayName = _localizationManager?.GetString("AddDrillArray");
            var addDrillRectName = _localizationManager?.GetString("AddDrillRect");
            var addDrillCircleName = _localizationManager?.GetString("AddDrillCircle");
            var addDrillPackageName = _localizationManager?.GetString("AddDrillPackage");
            var drillPointsName = _localizationManager?.GetString("DrillPointsName");

            if (!string.IsNullOrEmpty(addDrillLineName) && operationName == addDrillLineName)
            {
                using (var vm = GetViewModel<DrillLineOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillArrayName) && operationName == addDrillArrayName)
            {
                using (var vm = GetViewModel<DrillArrayOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillRectName) && operationName == addDrillRectName)
            {
                using (var vm = GetViewModel<DrillRectOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillCircleName) && operationName == addDrillCircleName)
            {
                using (var vm = GetViewModel<DrillCircleOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillPackageName) && operationName == addDrillPackageName)
            {
                using (var vm = GetViewModel<DrillPackageOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else
            {
                // По умолчанию открываем DrillPointsOperationViewModel
                using (var vm = GetViewModel<DrillPointsOperationViewModel>())
                {
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
        }

        private void UpdateOperationCommandsCanExecute()
        {
            (MoveOperationUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveOperationDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (EditOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}