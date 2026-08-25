using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    /// <summary>
    /// View-модель вкладки «Карман» (пункт 7.2 плана): добавляет операции
    /// карманов в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции
    /// операций нет: список отображает единую коллекцию.
    /// </summary>
    public class PocketOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly ObservableCollection<OperationBase> _allOperations;

        public PocketOperationsViewModel(ILocalizationManager localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));

            AddPocketRectangleCommand = new RelayCommand(AddPocketRectangle);
            AddPocketCircleCommand = new RelayCommand(AddPocketCircle);
            AddPocketEllipseCommand = new RelayCommand(AddPocketEllipse);
            AddPocketDxfCommand = new RelayCommand(AddPocketDxf);
        }

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
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddPocketCircle()
        {
            var op = new PocketCircleOperation();
            var name = _localizationManager?.GetString("PocketCircleName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddPocketEllipse()
        {
            var op = new PocketEllipseOperation();
            var name = _localizationManager?.GetString("PocketEllipseName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddPocketDxf()
        {
            var op = new PocketDxfOperation();
            var name = _localizationManager?.GetString("PocketDxfName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }
    }
}
