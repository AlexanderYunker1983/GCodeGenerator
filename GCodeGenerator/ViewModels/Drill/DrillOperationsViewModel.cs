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
    /// <summary>
    /// View-модель вкладки «Сверление» (пункт 7.2 плана): добавляет операции
    /// сверления в единую коллекцию MainViewModel.AllOperations и открывает
    /// диалоги операций через фабрику (пункт 7.3). Собственной коллекции нет —
    /// <see cref="Operations"/> — фильтрованное представление единой коллекции
    /// по категории.
    /// </summary>
    public class DrillOperationsViewModel : ViewModelBase
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly ObservableCollection<OperationBase> _allOperations;

        public DrillOperationsViewModel(ILocalizationManager localizationManager, IOperationEditorFactory operationEditorFactory, ObservableCollection<OperationBase> allOperations)
        {
            _localizationManager = localizationManager;
            _operationEditorFactory = operationEditorFactory ?? throw new ArgumentNullException(nameof(operationEditorFactory));
            _allOperations = allOperations ?? throw new ArgumentNullException(nameof(allOperations));

            Operations = new FilteredOperationsView(_allOperations, OperationCategory.Drill);

            AddDrillPointsCommand = new RelayCommand(AddDrillPoints);
            AddDrillLineCommand = new RelayCommand(AddDrillLine);
            AddDrillArrayCommand = new RelayCommand(AddDrillArray);
            AddDrillRectCommand = new RelayCommand(AddDrillRect);
            AddDrillCircleCommand = new RelayCommand(AddDrillCircle);
            AddDrillArcCommand = new RelayCommand(AddDrillArc);
            AddDrillPolygonCommand = new RelayCommand(AddDrillPolygon);
            AddDrillEllipseCommand = new RelayCommand(AddDrillEllipse);
            AddDrillPackageCommand = new RelayCommand(AddDrillPackage);
        }

        /// <summary>
        /// Фильтрованное представление единой коллекции операций
        /// (пункт 7.2 плана): только операции сверления, в порядке AllOperations.
        /// </summary>
        public FilteredOperationsView Operations { get; }

        /// <summary>
        /// Событие: пользователь добавил новую операцию через вкладку
        /// (MainViewModel выбирает её в общем списке).
        /// </summary>
        public event Action<OperationBase> OperationAdded;

        public ICommand AddDrillPointsCommand { get; }
        public ICommand AddDrillLineCommand { get; }
        public ICommand AddDrillArrayCommand { get; }
        public ICommand AddDrillRectCommand { get; }
        public ICommand AddDrillCircleCommand { get; }
        public ICommand AddDrillArcCommand { get; }
        public ICommand AddDrillPolygonCommand { get; }
        public ICommand AddDrillEllipseCommand { get; }
        public ICommand AddDrillPackageCommand { get; }

        private void AddDrillPoints()
        {
            var op = new DrillPointsOperation();
            var name = _localizationManager?.GetString("DrillPointsName");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillLine()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Line);
            var name = _localizationManager?.GetString("AddDrillLine");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillArray()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Array);
            var name = _localizationManager?.GetString("AddDrillArray");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillRect()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Rect);
            var name = _localizationManager?.GetString("AddDrillRect");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillCircle()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Circle);
            var name = _localizationManager?.GetString("AddDrillCircle");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillArc()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Arc);
            var name = _localizationManager?.GetString("AddDrillArc");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillPolygon()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Polygon);
            var name = _localizationManager?.GetString("AddDrillPolygon");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillEllipse()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Ellipse);
            var name = _localizationManager?.GetString("AddDrillEllipse");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }

        private void AddDrillPackage()
        {
            var op = DrillPointsOperation.CreateNew(DrillMode.Package);
            var name = _localizationManager?.GetString("AddDrillPackage");
            if (!string.IsNullOrEmpty(name))
                op.Name = name;

            _allOperations.Add(op);
            OperationAdded?.Invoke(op);
            _operationEditorFactory.ShowEditor(op, _allOperations);
        }
    }
}
