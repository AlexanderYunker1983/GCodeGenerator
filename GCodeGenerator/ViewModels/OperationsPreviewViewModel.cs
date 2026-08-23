using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// ViewModel of the 2D operations preview (plan item 6.3). Exposes a
    /// pure <see cref="OperationScene"/> (Core data, no WPF types) and
    /// forwards selection / editing / show-all to <see cref="MainViewModel"/>.
    /// The view's code-behind only renders the scene and handles the mouse.
    /// </summary>
    public class OperationsPreviewViewModel : ViewModelBase
    {
        private readonly MainViewModel _main;
        private OperationScene _scene;
        private OperationBase _selectedOperation;

        public OperationsPreviewViewModel(MainViewModel main)
        {
            _main = main ?? throw new ArgumentNullException(nameof(main));
            _scene = OperationSceneBuilder.Build(main.AllOperations);
            _selectedOperation = main.SelectedOperation;

            _main.OperationsChanged += RebuildScene;
            (_main.AllOperations as INotifyCollectionChanged).CollectionChanged += (s, e) => RebuildScene();
            _main.ShowAllRequested += OnShowAllRequested;
            _main.PropertyChanged += OnMainPropertyChanged;
        }

        /// <summary>The pure 2D scene of all operations (Core types only).</summary>
        public OperationScene Scene
        {
            get => _scene;
            private set
            {
                if (ReferenceEquals(value, _scene)) return;
                _scene = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Selected operation; two-way with <see cref="MainViewModel.SelectedOperation"/>.
        /// </summary>
        public OperationBase SelectedOperation
        {
            get => _selectedOperation;
            set
            {
                if (ReferenceEquals(value, _selectedOperation)) return;
                _selectedOperation = value;
                OnPropertyChanged();
                _main.SelectedOperation = value;
            }
        }

        /// <summary>
        /// Opens the editor of the selected operation
        /// (wraps <see cref="MainViewModel.EditOperationCommand"/>).
        /// </summary>
        public ICommand EditOperationCommand => _main.EditOperationCommand;

        /// <summary>Raised when "show all" is requested (fit the view to the scene).</summary>
        public event EventHandler ShowAllRequested;

        private void OnMainPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedOperation))
                SyncSelection();
        }

        private void SyncSelection()
        {
            if (ReferenceEquals(_selectedOperation, _main.SelectedOperation)) return;
            _selectedOperation = _main.SelectedOperation;
            OnPropertyChanged();
        }

        private void RebuildScene()
        {
            Scene = OperationSceneBuilder.Build(_main.AllOperations);
            SyncSelection();
        }

        private void OnShowAllRequested()
        {
            ShowAllRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
