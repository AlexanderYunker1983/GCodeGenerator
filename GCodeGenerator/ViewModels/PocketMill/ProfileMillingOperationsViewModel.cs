using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;
using YLocalization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileMillingOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfileMillingOperationsViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            Operations = new ObservableCollection<OperationBase>();
            
            AddProfileRectangleCommand = new RelayCommand(AddProfileRectangle);
            AddProfileCircleCommand = new RelayCommand(AddProfileCircle);
            AddProfileEllipseCommand = new RelayCommand(AddProfileEllipse);
            
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
        public ICommand AddProfileCircleCommand { get; }
        public ICommand AddProfileEllipseCommand { get; }
        
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

            using (var vm = GetViewModel<ProfileRectangleOperationViewModel>())
            {
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddProfileCircle()
        {
            var op = new ProfileCircleOperation();
            var name = _localizationManager?.GetString("ProfileCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<ProfileCircleOperationViewModel>())
            {
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }
        }

        private void AddProfileEllipse()
        {
            var op = new ProfileEllipseOperation();
            var name = _localizationManager?.GetString("ProfileEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            using (var vm = GetViewModel<ProfileEllipseOperationViewModel>())
            {
                vm.ProfileMillingOperationsViewModel = this;
                vm.Operation = op;
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
                using (var vm = GetViewModel<ProfileRectangleOperationViewModel>())
                {
                    vm.ProfileMillingOperationsViewModel = this;
                    vm.Operation = profileRectOp;
                    vm.ShowAsync();
                }
            }
            else if (SelectedOperation is ProfileCircleOperation profileCircleOp)
            {
                using (var vm = GetViewModel<ProfileCircleOperationViewModel>())
                {
                    vm.ProfileMillingOperationsViewModel = this;
                    vm.Operation = profileCircleOp;
                    vm.ShowAsync();
                }
            }
            else if (SelectedOperation is ProfileEllipseOperation profileEllipseOp)
            {
                using (var vm = GetViewModel<ProfileEllipseOperationViewModel>())
                {
                    vm.ProfileMillingOperationsViewModel = this;
                    vm.Operation = profileEllipseOp;
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

