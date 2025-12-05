using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class DrillPointsOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPointsOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("DrillPointsName");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по точкам" : title;

            Holes = new ObservableCollection<DrillHole>();

            AddHoleCommand = new RelayCommand(AddHole);
            RemoveHoleCommand = new RelayCommand(RemoveSelectedHole, () => SelectedHole != null);
            MoveHoleUpCommand = new RelayCommand(MoveSelectedHoleUp, CanMoveSelectedHoleUp);
            MoveHoleDownCommand = new RelayCommand(MoveSelectedHoleDown, CanMoveSelectedHoleDown);
        }

        private DrillPointsOperation _operation;

        public DrillPointsOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                // Sync existing holes from operation into local collection.
                Holes.Clear();
                if (_operation.Holes.Any())
                {
                    foreach (var hole in _operation.Holes)
                        Holes.Add(hole);
                    SelectedHole = Holes.FirstOrDefault();
                }
            }
        }

        public ObservableCollection<DrillHole> Holes { get; }

        private DrillHole _selectedHole;

        public DrillHole SelectedHole
        {
            get => _selectedHole;
            set
            {
                if (Equals(value, _selectedHole)) return;
                _selectedHole = value;
                OnPropertyChanged();
                UpdateCommands();
            }
        }

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

        public double FeedXYRapid
        {
            get => Operation?.FeedXYRapid ?? 0;
            set
            {
                if (Operation == null || value.Equals(Operation.FeedXYRapid)) return;
                Operation.FeedXYRapid = value;
                OnPropertyChanged();
            }
        }

        public double FeedXYWork
        {
            get => Operation?.FeedXYWork ?? 0;
            set
            {
                if (Operation == null || value.Equals(Operation.FeedXYWork)) return;
                Operation.FeedXYWork = value;
                OnPropertyChanged();
            }
        }

        public double SafeZBetweenHoles
        {
            get => Operation?.SafeZBetweenHoles ?? 0;
            set
            {
                if (Operation == null || value.Equals(Operation.SafeZBetweenHoles)) return;
                Operation.SafeZBetweenHoles = value;
                OnPropertyChanged();
            }
        }

        public int Decimals
        {
            get => Operation?.Decimals ?? 3;
            set
            {
                if (Operation == null || value == Operation.Decimals) return;
                Operation.Decimals = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddHoleCommand { get; }
        public ICommand RemoveHoleCommand { get; }
        public ICommand MoveHoleUpCommand { get; }
        public ICommand MoveHoleDownCommand { get; }

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

            _operation.Holes.Clear();
            foreach (var hole in Holes)
                _operation.Holes.Add(hole);
        }

        private void AddHole()
        {
            DrillHole newHole;
            if (Holes.Any())
            {
                var last = Holes.Last();
                newHole = new DrillHole
                {
                    X = last.X,
                    Y = last.Y,
                    Z = last.Z,
                    TotalDepth = last.TotalDepth,
                    StepDepth = last.StepDepth,
                    FeedZRapid = last.FeedZRapid,
                    FeedZWork = last.FeedZWork,
                    RetractHeight = last.RetractHeight
                };
            }
            else
            {
                // First hole defaults: Z = 0, rest as reasonable drilling defaults.
                newHole = new DrillHole
                {
                    X = 0,
                    Y = 0,
                    Z = 0,
                    TotalDepth = 2,
                    StepDepth = 1,
                    FeedZRapid = 500,
                    FeedZWork = 200,
                    RetractHeight = 0.3
                };
            }

            Holes.Add(newHole);
            SelectedHole = newHole;
        }

        private void RemoveSelectedHole()
        {
            if (SelectedHole == null) return;
            var index = Holes.IndexOf(SelectedHole);
            if (index < 0) return;
            Holes.RemoveAt(index);
            SelectedHole = index < Holes.Count ? Holes[index] : Holes.LastOrDefault();
        }

        private bool CanMoveSelectedHoleUp()
        {
            if (SelectedHole == null) return false;
            var index = Holes.IndexOf(SelectedHole);
            return index > 0;
        }

        private bool CanMoveSelectedHoleDown()
        {
            if (SelectedHole == null) return false;
            var index = Holes.IndexOf(SelectedHole);
            return index >= 0 && index < Holes.Count - 1;
        }

        private void MoveSelectedHoleUp()
        {
            if (!CanMoveSelectedHoleUp()) return;
            var index = Holes.IndexOf(SelectedHole);
            Holes.Move(index, index - 1);
        }

        private void MoveSelectedHoleDown()
        {
            if (!CanMoveSelectedHoleDown()) return;
            var index = Holes.IndexOf(SelectedHole);
            Holes.Move(index, index + 1);
        }

        private void UpdateCommands()
        {
            (RemoveHoleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveHoleUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MoveHoleDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}


