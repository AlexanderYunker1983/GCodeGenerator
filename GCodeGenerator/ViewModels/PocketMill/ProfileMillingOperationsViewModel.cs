using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.PocketMill
{
    /// <summary>
    /// View-модель вкладки «Профиль» (пункт 7.2 плана): добавляет операции
    /// профиля в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций. Собственной коллекции нет — <see cref="Operations"/>
    /// — фильтрованное представление единой коллекции по категории.
    /// </summary>
    public class ProfileMillingOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly ObservableCollection<OperationBase> _allOperations;

        public ProfileMillingOperationsViewModel(ILocalizationManager localizationManager, IDialogService dialogService, ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));

            Operations = new FilteredOperationsView(_allOperations, OperationCategory.Profile);

            AddProfileRectangleCommand = new RelayCommand(AddProfileRectangle);
            AddProfileRoundedRectangleCommand = new RelayCommand(AddProfileRoundedRectangle);
            AddProfileCircleCommand = new RelayCommand(AddProfileCircle);
            AddProfileEllipseCommand = new RelayCommand(AddProfileEllipse);
            AddProfilePolygonCommand = new RelayCommand(AddProfilePolygon);
            AddProfileDxfCommand = new RelayCommand(AddProfileDxf);
        }

        /// <summary>
        /// Фильтрованное представление единой коллекции операций
        /// (пункт 7.2 плана): только операции профиля, в порядке AllOperations.
        /// </summary>
        public FilteredOperationsView Operations { get; }

        /// <summary>
        /// Событие: пользователь добавил новую операцию через вкладку
        /// (MainViewModel выбирает её в общем списке).
        /// </summary>
        public event Action<OperationBase> OperationAdded;

        public ICommand AddProfileRectangleCommand { get; }
        public ICommand AddProfileRoundedRectangleCommand { get; }
        public ICommand AddProfileCircleCommand { get; }
        public ICommand AddProfileEllipseCommand { get; }
        public ICommand AddProfilePolygonCommand { get; }
        public ICommand AddProfileDxfCommand { get; }

        private void AddProfileRectangle()
        {
            var op = new ProfileRectangleOperation();
            var name = _localizationManager?.GetString("ProfileRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfileRectangleOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddProfileRoundedRectangle()
        {
            var op = new ProfileRoundedRectangleOperation();
            var name = _localizationManager?.GetString("ProfileRoundedRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfileRoundedRectangleOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddProfileCircle()
        {
            var op = new ProfileCircleOperation();
            var name = _localizationManager?.GetString("ProfileCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfileCircleOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddProfileEllipse()
        {
            var op = new ProfileEllipseOperation();
            var name = _localizationManager?.GetString("ProfileEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfileEllipseOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddProfilePolygon()
        {
            var op = new ProfilePolygonOperation();
            var name = _localizationManager?.GetString("ProfilePolygonName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfilePolygonOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }

        private void AddProfileDxf()
        {
            var op = new ProfileDxfOperation();
            var name = _localizationManager?.GetString("ProfileDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);

            var vm = _dialogService.CreateViewModel<ProfileDxfOperationViewModel>();
            vm.Operations = _allOperations;
            vm.Operation = op;
            _dialogService.ShowDialog(vm);
        }
    }
}
