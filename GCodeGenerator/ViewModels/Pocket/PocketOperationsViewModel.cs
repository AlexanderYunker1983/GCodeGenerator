using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketOperationsViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;

        public PocketOperationsViewModel(ILocalizationManager localizationManager, IDialogService dialogService)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            var title = _localizationManager?.GetString("PocketTab") ?? "Карман";
            DisplayName = title;

            Operations = new ObservableCollection<OperationBase>();
            AddPocketRectangleCommand = new RelayCommand(AddPocketRectangle);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, () => SelectedOperation != null);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, () => SelectedOperation != null);
            AddPocketCircleCommand = new RelayCommand(AddPocketCircle);
            AddPocketEllipseCommand = new RelayCommand(AddPocketEllipse);
            AddPocketDxfCommand = new RelayCommand(AddPocketDxf);
        }

        public ViewModels.MainViewModel MainViewModel { get; set; }

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
                if (MainViewModel != null && value != null)
                    MainViewModel.SelectedOperation = value;
                (EditOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (RemoveOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }

        public ICommand AddPocketRectangleCommand { get; }
        public ICommand EditOperationCommand { get; }
        public ICommand RemoveOperationCommand { get; }
        public ICommand AddPocketCircleCommand { get; }
        public ICommand AddPocketEllipseCommand { get; }
        public ICommand AddPocketDxfCommand { get; }

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

        private void AddPocketRectangle()
        {
            var op = new PocketRectangleOperation();
            var name = _localizationManager?.GetString("PocketRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<PocketRectangleOperationViewModel>();
            vm.PocketOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);

            if (MainViewModel != null)
            {
                if (!MainViewModel.AllOperations.Contains(op))
                    MainViewModel.AllOperations.Add(op);
                MainViewModel.NotifyOperationsChanged();
            }
        }

        private void AddPocketCircle()
        {
            var op = new PocketCircleOperation();
            var name = _localizationManager?.GetString("PocketCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<PocketCircleOperationViewModel>();
            vm.PocketOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);

            if (MainViewModel != null)
            {
                if (!MainViewModel.AllOperations.Contains(op))
                    MainViewModel.AllOperations.Add(op);
                MainViewModel.NotifyOperationsChanged();
            }
        }

        private void AddPocketEllipse()
        {
            var op = new PocketEllipseOperation();
            var name = _localizationManager?.GetString("PocketEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<PocketEllipseOperationViewModel>();
            vm.PocketOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);

            if (MainViewModel != null)
            {
                if (!MainViewModel.AllOperations.Contains(op))
                    MainViewModel.AllOperations.Add(op);
                MainViewModel.NotifyOperationsChanged();
            }
        }

        public void RemoveOperation(OperationBase operation)
        {
            if (operation == null) return;
            var idx = Operations.IndexOf(operation);
            if (idx < 0) return;
            Operations.RemoveAt(idx);
            if (SelectedOperation == operation)
                SelectedOperation = idx < Operations.Count ? Operations[idx] : null;
            if (MainViewModel != null)
            {
                MainViewModel.AllOperations.Remove(operation);
                MainViewModel.NotifyOperationsChanged();
            }
            (EditOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (RemoveOperationCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }

        public void RemoveSelectedOperation()
        {
            RemoveOperation(SelectedOperation);
        }

        private void AddPocketDxf()
        {
            var op = new PocketDxfOperation();
            var name = _localizationManager?.GetString("PocketDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            Operations.Add(op);
            SelectedOperation = op;

            var vm = _dialogService.CreateViewModel<PocketDxfOperationViewModel>();
            vm.PocketOperationsViewModel = this;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);

            if (MainViewModel != null)
            {
                if (!MainViewModel.AllOperations.Contains(op))
                    MainViewModel.AllOperations.Add(op);
                MainViewModel.NotifyOperationsChanged();
            }
        }

        public void EditSelectedOperation()
        {
            if (SelectedOperation is PocketRectangleOperation pocketRect)
            {
                var vm = _dialogService.CreateViewModel<PocketRectangleOperationViewModel>();
                vm.PocketOperationsViewModel = this;
                vm.Operation = pocketRect;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is PocketCircleOperation pocketCircle)
            {
                var vm = _dialogService.CreateViewModel<PocketCircleOperationViewModel>();
                vm.PocketOperationsViewModel = this;
                vm.Operation = pocketCircle;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is PocketEllipseOperation pocketEllipse)
            {
                var vm = _dialogService.CreateViewModel<PocketEllipseOperationViewModel>();
                vm.PocketOperationsViewModel = this;
                vm.Operation = pocketEllipse;
                _dialogService.ShowDialog(vm);
            }
            else if (SelectedOperation is PocketDxfOperation pocketDxf)
            {
                var vm = _dialogService.CreateViewModel<PocketDxfOperationViewModel>();
                vm.PocketOperationsViewModel = this;
                vm.Operation = pocketDxf;
                _dialogService.ShowDialog(vm);
            }
        }
    }
}



