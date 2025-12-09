using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using MugenMvvmToolkit.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;
using YLocalization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillOperationsViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            Operations = new ObservableCollection<OperationBase>();
            
            AddDrillPointsCommand = new RelayCommand(AddDrillPoints);
            AddDrillLineCommand = new RelayCommand(AddDrillLine);
            AddDrillArrayCommand = new RelayCommand(AddDrillArray);
            AddDrillRectCommand = new RelayCommand(AddDrillRect);
            AddDrillCircleCommand = new RelayCommand(AddDrillCircle);
            AddDrillEllipseCommand = new RelayCommand(AddDrillEllipse);
            AddDrillPackageCommand = new RelayCommand(AddDrillPackage);
            
            MoveOperationUpCommand = new RelayCommand(MoveSelectedOperationUp, CanMoveSelectedOperationUp);
            MoveOperationDownCommand = new RelayCommand(MoveSelectedOperationDown, CanMoveSelectedOperationDown);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, CanModifySelectedOperation);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, CanModifySelectedOperation);
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
                
                // Notify parent ViewModel if needed
                if (MainViewModel != null && value != null)
                {
                    MainViewModel.SelectedOperation = value;
                }
            }
        }
        
        public MainViewModel MainViewModel { get; set; }

        public ICommand AddDrillPointsCommand { get; }
        public ICommand AddDrillLineCommand { get; }
        public ICommand AddDrillArrayCommand { get; }
        public ICommand AddDrillRectCommand { get; }
        public ICommand AddDrillCircleCommand { get; }
        public ICommand AddDrillEllipseCommand { get; }
        public ICommand AddDrillPackageCommand { get; }
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

            using (var vm = GetViewModel<DrillPointsOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillLine()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillLine");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillLineOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillArray()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillArray");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillArrayOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillRect()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillRect");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillRectOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillCircle()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillCircle");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillCircleOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillEllipse()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillEllipse");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillEllipseOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillPackage()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("AddDrillPackage");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<DrillPackageOperationViewModel>())
            {
                vm.MainViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
            MainViewModel?.NotifyOperationsChanged();
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

        public void MoveSelectedOperationUp()
        {
            if (!CanMoveSelectedOperationUp()) return;
            var index = Operations.IndexOf(SelectedOperation);
            Operations.Move(index, index - 1);
            UpdateOperationCommandsCanExecute();
        }

        public void MoveSelectedOperationDown()
        {
            if (!CanMoveSelectedOperationDown()) return;
            var index = Operations.IndexOf(SelectedOperation);
            Operations.Move(index, index + 1);
            UpdateOperationCommandsCanExecute();
        }

        public void RemoveSelectedOperation()
        {
            if (!CanModifySelectedOperation()) return;
            var index = Operations.IndexOf(SelectedOperation);
            if (index < 0) return;
            Operations.RemoveAt(index);
            SelectedOperation = index < Operations.Count ? Operations[index] : null;
            UpdateOperationCommandsCanExecute();
        }

        public void RemoveOperation(OperationBase operation)
        {
            if (operation == null) return;
            var index = Operations.IndexOf(operation);
            if (index < 0) return;
            Operations.RemoveAt(index);
            if (SelectedOperation == operation)
            {
                SelectedOperation = index < Operations.Count ? Operations[index] : null;
            }
            UpdateOperationCommandsCanExecute();
        }

        public void EditSelectedOperation()
        {
            if (!(SelectedOperation is DrillPointsOperation drillOp))
                return;

            // Определяем тип операции по имени
            var operationName = drillOp.Name;
            var addDrillLineName = _localizationManager?.GetString("AddDrillLine");
            var addDrillArrayName = _localizationManager?.GetString("AddDrillArray");
            var addDrillRectName = _localizationManager?.GetString("AddDrillRect");
            var addDrillCircleName = _localizationManager?.GetString("AddDrillCircle");
            var addDrillEllipseName = _localizationManager?.GetString("AddDrillEllipse");
            var addDrillPackageName = _localizationManager?.GetString("AddDrillPackage");
            var drillPointsName = _localizationManager?.GetString("DrillPointsName");

            if (!string.IsNullOrEmpty(addDrillLineName) && operationName == addDrillLineName)
            {
                using (var vm = GetViewModel<DrillLineOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillArrayName) && operationName == addDrillArrayName)
            {
                using (var vm = GetViewModel<DrillArrayOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillRectName) && operationName == addDrillRectName)
            {
                using (var vm = GetViewModel<DrillRectOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillCircleName) && operationName == addDrillCircleName)
            {
                using (var vm = GetViewModel<DrillCircleOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillEllipseName) && operationName == addDrillEllipseName)
            {
                using (var vm = GetViewModel<DrillEllipseOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else if (!string.IsNullOrEmpty(addDrillPackageName) && operationName == addDrillPackageName)
            {
                using (var vm = GetViewModel<DrillPackageOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }
            else
            {
                // По умолчанию открываем DrillPointsOperationViewModel
                using (var vm = GetViewModel<DrillPointsOperationViewModel>())
                {
                    vm.MainViewModel = this;
                    vm.Operation = drillOp;
                    vm.ShowAsync();
                }
            }

            MainViewModel?.NotifyOperationsChanged();
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

