using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillPointsOperationViewModel : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPointsOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("DrillPointsName") ?? "DrillPointsName";

            Holes = new ObservableCollection<DrillHole>();

            AddHoleCommand = new RelayCommand(AddHole);
            RemoveHoleCommand = new RelayCommand(RemoveSelectedHole, () => SelectedHole != null);
            MoveHoleUpCommand = new RelayCommand(MoveSelectedHoleUp, CanMoveSelectedHoleUp);
            MoveHoleDownCommand = new RelayCommand(MoveSelectedHoleDown, CanMoveSelectedHoleDown);
        }

        protected override void LoadFromOperation(DrillPointsOperation operation)
        {
            // Sync existing holes from operation into local collection.
            Holes.Clear();
            if (operation.Holes.Any())
            {
                foreach (var hole in operation.Holes)
                    Holes.Add(hole);
                SelectedHole = Holes.FirstOrDefault();
            }
            else
            {
                // Create first default hole if list is empty
                var defaultHole = new DrillHole
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
                Holes.Add(defaultHole);
                SelectedHole = defaultHole;
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

        protected override void ApplyToOperation()
        {
            // Save holes to operation (пункт 3.3: режим фиксируется в DrillMode).
            Operation.DrillMode = DrillMode.Points;
            Operation.Holes.Clear();
            foreach (var hole in Holes)
                Operation.Holes.Add(hole);
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // сверление без отверстий не имеет смысла.
        protected override bool IsValid() => Holes.Count > 0;

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
            (RemoveHoleCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveHoleUpCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (MoveHoleDownCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}


