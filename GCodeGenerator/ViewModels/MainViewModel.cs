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
            GenerateGCodeCommand = new RelayCommand(GenerateGCode, () => Operations.Count > 0);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            MoveOperationUpCommand = new RelayCommand(MoveSelectedOperationUp, CanMoveSelectedOperationUp);
            MoveOperationDownCommand = new RelayCommand(MoveSelectedOperationDown, CanMoveSelectedOperationDown);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, CanModifySelectedOperation);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, CanModifySelectedOperation);

            var title = _localizationManager?.GetString("MainTitle");
            _displayName = string.IsNullOrEmpty(title) ? "Генератор G-кода" : title;
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
            }
        }

        public ICommand AddDrillPointsCommand { get; }

        public ICommand AddDrillLineCommand { get; }

        public ICommand GenerateGCodeCommand { get; }

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

        private void GenerateGCode()
        {
            var program = _generator.Generate(new System.Collections.Generic.List<OperationBase>(Operations), _settings);
            var sb = new StringBuilder();
            foreach (var line in program.Lines)
                sb.AppendLine(line);
            GCodePreview = sb.ToString();
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

            using (var vm = GetViewModel<DrillPointsOperationViewModel>())
            {
                vm.Operation = drillOp;
                vm.ShowAsync();
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