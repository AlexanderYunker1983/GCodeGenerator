using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketOperationsViewModel : ViewModelBase, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public PocketOperationsViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("PocketTab") ?? "Карман";
            DisplayName = title;

            Operations = new ObservableCollection<OperationBase>();
            AddPocketRectangleCommand = new RelayCommand(AddPocketRectangle);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, () => SelectedOperation != null);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, () => SelectedOperation != null);
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
                (EditOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RemoveOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand AddPocketRectangleCommand { get; }
        public ICommand EditOperationCommand { get; }
        public ICommand RemoveOperationCommand { get; }

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

            using (var vm = GetViewModel<PocketRectangleOperationViewModel>())
            {
                vm.PocketOperationsViewModel = this;
                vm.Operation = op;
                vm.ShowAsync();
            }

            MainViewModel?.NotifyOperationsChanged();
        }

        public void RemoveOperation(OperationBase operation)
        {
            if (operation == null) return;
            var idx = Operations.IndexOf(operation);
            if (idx < 0) return;
            Operations.RemoveAt(idx);
            if (SelectedOperation == operation)
                SelectedOperation = idx < Operations.Count ? Operations[idx] : null;
            MainViewModel?.NotifyOperationsChanged();
            (EditOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RemoveOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void RemoveSelectedOperation()
        {
            RemoveOperation(SelectedOperation);
        }

        public void EditSelectedOperation()
        {
            if (SelectedOperation is PocketRectangleOperation pocketRect)
            {
                using (var vm = GetViewModel<PocketRectangleOperationViewModel>())
                {
                    vm.PocketOperationsViewModel = this;
                    vm.Operation = pocketRect;
                    vm.ShowAsync();
                }
            }
        }
    }
}



