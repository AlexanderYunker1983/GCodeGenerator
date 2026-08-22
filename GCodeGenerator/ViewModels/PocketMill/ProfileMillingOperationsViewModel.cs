using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileMillingOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;

        public ProfileMillingOperationsViewModel(ILocalizationManager localizationManager, IDialogService dialogService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            Operations = new ObservableCollection<OperationBase>();
            
            AddProfileRectangleCommand = new RelayCommand(AddProfileRectangle);
            AddProfileRoundedRectangleCommand = new RelayCommand(AddProfileRoundedRectangle);
            AddProfileCircleCommand = new RelayCommand(AddProfileCircle);
            AddProfileEllipseCommand = new RelayCommand(AddProfileEllipse);
            AddProfilePolygonCommand = new RelayCommand(AddProfilePolygon);
            AddProfileDxfCommand = new RelayCommand(AddProfileDxf);
            
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
        
        public ViewModels.MainViewModel MainViewModel { get; set; }

        public ICommand AddProfileRectangleCommand { get; }
        public ICommand AddProfileRoundedRectangleCommand { get; }
        public ICommand AddProfileCircleCommand { get; }
        public ICommand AddProfileEllipseCommand { get; }
        public ICommand AddProfilePolygonCommand { get; }
        public ICommand AddProfileDxfCommand { get; }
        
        public ICommand MoveOperationUpCommand { get; }
        public ICommand MoveOperationDownCommand { get; }
        public ICommand RemoveOperationCommand { get; }
        public ICommand EditOperationCommand { get; }

        private void AddProfileRectangle()
        {
            var op = new ProfileRectangleOperation();
            var name = _localizationManager?.GetString("ProfileRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfileRectangleOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddProfileRoundedRectangle()
        {
            var op = new ProfileRoundedRectangleOperation();
            var name = _localizationManager?.GetString("ProfileRoundedRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfileRoundedRectangleOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddProfileCircle()
        {
            var op = new ProfileCircleOperation();
            var name = _localizationManager?.GetString("ProfileCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfileCircleOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddProfileEllipse()
        {
            var op = new ProfileEllipseOperation();
            var name = _localizationManager?.GetString("ProfileEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfileEllipseOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddProfilePolygon()
        {
            var op = new ProfilePolygonOperation();
            var name = _localizationManager?.GetString("ProfilePolygonName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfilePolygonOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
            MainViewModel?.NotifyOperationsChanged();
        }

        private void AddProfileDxf()
        {
            var op = new ProfileDxfOperation();
            var name = _localizationManager?.GetString("ProfileDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<ProfileDxfOperationViewModel>();
            vm.ProfileMillingOperationsViewModel = this;
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

        public void EditSelectedOperation()
        {
            if (SelectedOperation is ProfileRectangleOperation profileRectOp)
            {
                var vm = _dialogService.CreateViewModel<ProfileRectangleOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = profileRectOp;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is ProfileRoundedRectangleOperation roundedOp)
            {
                var vm = _dialogService.CreateViewModel<ProfileRoundedRectangleOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = roundedOp;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is ProfileCircleOperation profileCircleOp)
            {
                var vm = _dialogService.CreateViewModel<ProfileCircleOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = profileCircleOp;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is ProfileEllipseOperation profileEllipseOp)
            {
                var vm = _dialogService.CreateViewModel<ProfileEllipseOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = profileEllipseOp;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is ProfilePolygonOperation profilePolygonOp)
            {
                var vm = _dialogService.CreateViewModel<ProfilePolygonOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = profilePolygonOp;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is ProfileDxfOperation profileDxfOp)
            {
                var vm = _dialogService.CreateViewModel<ProfileDxfOperationViewModel>();
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = profileDxfOp;
                _dialogService.ShowDialog(vm);
            }

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

