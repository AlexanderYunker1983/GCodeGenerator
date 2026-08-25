using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
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
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции
    /// операций нет: список отображает единую коллекцию.
    /// </summary>
    public class ProfileMillingOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly ObservableCollection<OperationBase> _allOperations;

        public ProfileMillingOperationsViewModel(ILocalizationManager localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));

            AddProfileRectangleCommand = new RelayCommand(AddProfileRectangle);
            AddProfileRoundedRectangleCommand = new RelayCommand(AddProfileRoundedRectangle);
            AddProfileCircleCommand = new RelayCommand(AddProfileCircle);
            AddProfileEllipseCommand = new RelayCommand(AddProfileEllipse);
            AddProfilePolygonCommand = new RelayCommand(AddProfilePolygon);
            AddProfileDxfCommand = new RelayCommand(AddProfileDxf);
        }

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

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }

        private void AddProfileRoundedRectangle()
        {
            var op = new ProfileRoundedRectangleOperation();
            var name = _localizationManager?.GetString("ProfileRoundedRectangleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }

        private void AddProfileCircle()
        {
            var op = new ProfileCircleOperation();
            var name = _localizationManager?.GetString("ProfileCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }

        private void AddProfileEllipse()
        {
            var op = new ProfileEllipseOperation();
            var name = _localizationManager?.GetString("ProfileEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }

        private void AddProfilePolygon()
        {
            var op = new ProfilePolygonOperation();
            var name = _localizationManager?.GetString("ProfilePolygonName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }

        private void AddProfileDxf()
        {
            var op = new ProfileDxfOperation();
            var name = _localizationManager?.GetString("ProfileDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            if (_operationEditorFactory.CreateOperation(op, _allOperations))
                OperationAdded?.Invoke(op);
        }
    }
}
