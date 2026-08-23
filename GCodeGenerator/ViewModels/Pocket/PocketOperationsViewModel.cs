using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// View-модель вкладки «Карман» (пункт 7.2 плана): добавляет операции
    /// карманов в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций. Собственной коллекции нет — <see cref="Operations"/>
    /// — фильтрованное представление единой коллекции по категории.
    /// </summary>
    public class PocketOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly ObservableCollection<OperationBase> _allOperations;

        public PocketOperationsViewModel(ILocalizationManager localizationManager, IDialogService dialogService, ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));

            Operations = new FilteredOperationsView(_allOperations, OperationCategory.Pocket);

            AddPocketRectangleCommand = new RelayCommand(AddPocketRectangle);
            AddPocketCircleCommand = new RelayCommand(AddPocketCircle);
            AddPocketEllipseCommand = new RelayCommand(AddPocketEllipse);
            AddPocketDxfCommand = new RelayCommand(AddPocketDxf);
        }

        /// <summary>
        /// Фильтрованное представление единой коллекции операций
        /// (пункт 7.2 плана): только операции карманов, в порядке AllOperations.
        /// </summary>
        public FilteredOperationsView Operations { get; }

        /// <summary>
        /// Событие: пользователь добавил новую операцию через вкладку
        /// (MainViewModel выбирает её в общем списке).
        /// </summary>
        public event Action<OperationBase> OperationAdded;

        public ICommand AddPocketRectangleCommand { get; }
        public ICommand AddPocketCircleCommand { get; }
        public ICommand AddPocketEllipseCommand { get; }
        public ICommand AddPocketDxfCommand { get; }

        private void AddPocketRectangle()
        {
            var op = new PocketRectangleOperation();
            var name = _localizationManager?.GetString("PocketRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<PocketRectangleOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddPocketCircle()
        {
            var op = new PocketCircleOperation();
            var name = _localizationManager?.GetString("PocketCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<PocketCircleOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddPocketEllipse()
        {
            var op = new PocketEllipseOperation();
            var name = _localizationManager?.GetString("PocketEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<PocketEllipseOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddPocketDxf()
        {
            var op = new PocketDxfOperation();
            var name = _localizationManager?.GetString("PocketDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<PocketDxfOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }
    }
}
