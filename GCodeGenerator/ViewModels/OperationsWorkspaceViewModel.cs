using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Owns the operation workspace: the single operation collection, category
    /// views, selection, editing/reordering commands and the synchronized 2D preview.
    /// </summary>
    public sealed class OperationsWorkspaceViewModel : ViewModelBase
    {
        private readonly IOperationEditorFactory _operationEditorFactory;
        private readonly HashSet<OperationBase> _attachedOperations = new HashSet<OperationBase>();
        private OperationBase _selectedOperation;

        public OperationsWorkspaceViewModel(
            ILocalizationManager localizationManager,
            IOperationEditorFactory operationEditorFactory,
            IThemeService themeService)
        {
            _operationEditorFactory = operationEditorFactory
                ?? throw new ArgumentNullException(nameof(operationEditorFactory));

            AllOperations = new ObservableCollection<OperationBase>();
            AllOperations.CollectionChanged += OnAllOperationsCollectionChanged;

            DrillOperations = new DrillOperationsViewModel(
                localizationManager,
                operationEditorFactory,
                AllOperations);
            DrillOperations.OperationAdded += OnCategoryOperationAdded;
            ProfileMillingOperations = new ProfileMillingOperationsViewModel(
                localizationManager,
                operationEditorFactory,
                AllOperations);
            ProfileMillingOperations.OperationAdded += OnCategoryOperationAdded;
            PocketOperations = new PocketOperationsViewModel(
                localizationManager,
                operationEditorFactory,
                AllOperations);
            PocketOperations.OperationAdded += OnCategoryOperationAdded;

            OperationsPreview = new OperationsPreviewViewModel(AllOperations, themeService);
            OperationsPreview.SelectionChanged += OnPreviewSelectionChanged;
            OperationsPreview.EditRequested += OnPreviewEditRequested;

            ShowAllPreviewCommand = new RelayCommand(OperationsPreview.RaiseShowAll);
            MoveOperationUpCommand = new RelayCommand(MoveSelectedOperationUp, CanMoveSelectedOperationUp);
            MoveOperationDownCommand = new RelayCommand(MoveSelectedOperationDown, CanMoveSelectedOperationDown);
            RemoveOperationCommand = new RelayCommand(RemoveSelectedOperation, CanModifySelectedOperation);
            EditOperationCommand = new RelayCommand(EditSelectedOperation, CanModifySelectedOperation);
        }

        /// <summary>Raised for collection or operation-content changes, never for selection only.</summary>
        public event EventHandler ContentChanged;

        public DrillOperationsViewModel DrillOperations { get; }

        public ProfileMillingOperationsViewModel ProfileMillingOperations { get; }

        public PocketOperationsViewModel PocketOperations { get; }

        public OperationsPreviewViewModel OperationsPreview { get; }

        public ObservableCollection<OperationBase> AllOperations { get; }

        public OperationBase SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (Equals(value, _selectedOperation)) return;
                _selectedOperation = value;
                OnPropertyChanged();
                UpdateOperationCommandsCanExecute();
                OperationsPreview.SelectedOperation = value;
            }
        }

        public ICommand ShowAllPreviewCommand { get; }

        public ICommand MoveOperationUpCommand { get; }

        public ICommand MoveOperationDownCommand { get; }

        public ICommand RemoveOperationCommand { get; }

        public ICommand EditOperationCommand { get; }

        public void NotifyOperationsChanged()
        {
            OperationsPreview.RebuildScene();
        }

        private void OnAllOperationsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e?.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var operation in new List<OperationBase>(_attachedOperations))
                    DetachOperation(operation);

                if (SelectedOperation != null && !AllOperations.Contains(SelectedOperation))
                    SelectedOperation = null;

                foreach (var operation in AllOperations)
                    AttachOperation(operation);
            }
            // Move reports the same item in both OldItems and NewItems. Its
            // subscription and selection must remain intact.
            else if (e?.Action != NotifyCollectionChangedAction.Move)
            {
                if (e?.OldItems != null)
                {
                    foreach (OperationBase operation in e.OldItems)
                    {
                        if (!AllOperations.Contains(operation))
                            DetachOperation(operation);
                        if (ReferenceEquals(SelectedOperation, operation) &&
                            !AllOperations.Contains(operation))
                            SelectedOperation = null;
                    }
                }

                if (e?.NewItems != null)
                {
                    foreach (OperationBase operation in e.NewItems)
                        AttachOperation(operation);
                }
            }

            UpdateOperationCommandsCanExecute();
            NotifyOperationsChanged();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AttachOperation(OperationBase operation)
        {
            if (operation == null) return;
            if (_attachedOperations.Add(operation))
                operation.PropertyChanged += OnOperationPropertyChanged;
        }

        private void DetachOperation(OperationBase operation)
        {
            if (operation == null) return;
            if (_attachedOperations.Remove(operation))
                operation.PropertyChanged -= OnOperationPropertyChanged;
        }

        private void OnOperationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyOperationsChanged();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnPreviewSelectionChanged(object sender, OperationBase operation)
        {
            SelectedOperation = operation;
        }

        private void OnPreviewEditRequested(object sender, EventArgs e)
        {
            if (CanModifySelectedOperation())
                EditSelectedOperation();
        }

        private void OnCategoryOperationAdded(OperationBase operation)
        {
            SelectedOperation = operation;
        }

        private bool CanModifySelectedOperation() => SelectedOperation != null;

        private bool CanMoveSelectedOperationUp()
        {
            if (SelectedOperation == null || AllOperations.Count < 2) return false;
            return AllOperations.IndexOf(SelectedOperation) > 0;
        }

        private bool CanMoveSelectedOperationDown()
        {
            if (SelectedOperation == null) return false;
            var index = AllOperations.IndexOf(SelectedOperation);
            return index >= 0 && index < AllOperations.Count - 1;
        }

        private void MoveSelectedOperationUp()
        {
            if (!CanMoveSelectedOperationUp()) return;
            var index = AllOperations.IndexOf(SelectedOperation);
            AllOperations.Move(index, index - 1);
        }

        private void MoveSelectedOperationDown()
        {
            if (!CanMoveSelectedOperationDown()) return;
            var index = AllOperations.IndexOf(SelectedOperation);
            AllOperations.Move(index, index + 1);
        }

        private void RemoveSelectedOperation()
        {
            if (CanModifySelectedOperation())
                AllOperations.Remove(SelectedOperation);
        }

        private void EditSelectedOperation()
        {
            if (SelectedOperation != null)
                _operationEditorFactory.ShowEditor(SelectedOperation, AllOperations);
        }

        private void UpdateOperationCommandsCanExecute()
        {
            ((RelayCommand)MoveOperationUpCommand).NotifyCanExecuteChanged();
            ((RelayCommand)MoveOperationDownCommand).NotifyCanExecuteChanged();
            ((RelayCommand)RemoveOperationCommand).NotifyCanExecuteChanged();
            ((RelayCommand)EditOperationCommand).NotifyCanExecuteChanged();
        }
    }
}
