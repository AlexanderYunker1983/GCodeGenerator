using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;

        public DrillOperationsViewModel(ILocalizationManager localizationManager, IDialogService dialogService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            Operations = new ObservableCollection<OperationBase>();
            
            AddDrillPointsCommand = new RelayCommand(AddDrillPoints);
            AddDrillLineCommand = new RelayCommand(AddDrillLine);
            AddDrillArrayCommand = new RelayCommand(AddDrillArray);
            AddDrillRectCommand = new RelayCommand(AddDrillRect);
            AddDrillCircleCommand = new RelayCommand(AddDrillCircle);
            AddDrillArcCommand = new RelayCommand(AddDrillArc);
            AddDrillPolygonCommand = new RelayCommand(AddDrillPolygon);
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
        public ICommand AddDrillArcCommand { get; }
        public ICommand AddDrillPolygonCommand { get; }
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

            var vm = _dialogService.CreateViewModel<DrillPointsOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillLine()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Line);
            var name = _localizationManager?.GetString("AddDrillLine");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillLineOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillArray()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Array);
            var name = _localizationManager?.GetString("AddDrillArray");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillArrayOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillRect()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Rect);
            var name = _localizationManager?.GetString("AddDrillRect");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillRectOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillCircle()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Circle);
            var name = _localizationManager?.GetString("AddDrillCircle");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillCircleOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillArc()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Arc);
            var name = _localizationManager?.GetString("AddDrillArc");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillArcOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillPolygon()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Polygon);
            var name = _localizationManager?.GetString("AddDrillPolygon");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillPolygonOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillEllipse()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Ellipse);
            var name = _localizationManager?.GetString("AddDrillEllipse");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillEllipseOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddDrillPackage()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Package);
            var name = _localizationManager?.GetString("AddDrillPackage");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<DrillPackageOperationViewModel>();
            vm.MainViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
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

        /// <summary>
        /// Тип диалоговой view-модели для режима сверления (пункт 3.4 плана):
        /// диспетчеризация по <see cref="DrillMode"/>, а не по имени операции.
        /// </summary>
        public Type GetDialogViewModelType(DrillMode mode)
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

        public void EditSelectedOperation()
        {
            if (!(SelectedOperation is DrillPointsOperation drillOp))
                return;

            // Открываем диалог по режиму операции (пункт 3.4), а не по её имени.
            var vmType = GetDialogViewModelType(drillOp.DrillMode);
            var vm = (IDrillDialogViewModel)_dialogService.CreateViewModel(vmType);
            vm.MainViewModel = this;
            vm.Operation = drillOp;
            _dialogService.ShowDialog(vmType, vm);

            MainViewModel?.NotifyOperationsChanged();
        }

        private void UpdateOperationCommandsCanExecute()
        {
            (MoveOperationUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveOperationDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (RemoveOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (EditOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}

