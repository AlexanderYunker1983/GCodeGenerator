#nullable enable
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;

namespace GCodeGenerator.ViewModels.Drill
{
    /// <summary>
    /// Диалог сверления по точкам: отверстия задаются поштучно в таблице.
    ///
    /// В отличие от шаблонов, список отверстий здесь не рассчитывается,
    /// а составляется пользователем, поэтому таблица правит отверстия самой
    /// операции, а окно добавляет команды управления списком.
    /// </summary>
    public partial class DrillPointsOperationViewModel
        : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName
    {
        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveHoleCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveHoleUpCommand))]
        [NotifyCanExecuteChangedFor(nameof(MoveHoleDownCommand))]
        private DrillHole? _selectedHole;

        public DrillPointsOperationViewModel(ILocalizationManager localizationManager)
        {
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("DrillPointsName") ?? "DrillPointsName";

            AddHoleCommand = new RelayCommand(AddHole);
            RemoveHoleCommand = new RelayCommand(RemoveSelectedHole, () => SelectedHole != null);
            MoveHoleUpCommand = new RelayCommand(MoveSelectedHoleUp, CanMoveSelectedHoleUp);
            MoveHoleDownCommand = new RelayCommand(MoveSelectedHoleDown, CanMoveSelectedHoleDown);
        }

        public ICommand AddHoleCommand { get; }

        public IRelayCommand RemoveHoleCommand { get; }

        public IRelayCommand MoveHoleUpCommand { get; }

        public IRelayCommand MoveHoleDownCommand { get; }

        protected override void OnOperationChanged(DrillPointsOperation operation)
        {
            base.OnOperationChanged(operation);

            operation.DrillMode = DrillMode.Points;

            // У новой операции отверстий нет: одно пустое даёт таблице
            // строку, с которой можно начать.
            if (operation.Holes.Count == 0)
                operation.Holes.Add(DefaultHole());

            SelectedHole = operation.Holes.FirstOrDefault();
        }

        /// <summary>Сверление без отверстий не имеет смысла.</summary>
        protected override bool IsValid(DrillPointsOperation operation) => operation.Holes.Count > 0;

        private static DrillHole DefaultHole()
            => new DrillHole
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

        /// <summary>
        /// Новое отверстие повторяет последнее: подряд сверлят обычно
        /// одинаковые отверстия, меняя только координаты.
        /// </summary>
        private void AddHole()
        {
            if (Operation == null)
                return;

            var last = Operation.Holes.LastOrDefault();
            var hole = last == null
                ? DefaultHole()
                : new DrillHole
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

            Operation.Holes.Add(hole);
            SelectedHole = hole;
        }

        private void RemoveSelectedHole()
        {
            if (Operation == null || SelectedHole == null) return;
            var index = Operation.Holes.IndexOf(SelectedHole);
            if (index < 0) return;

            Operation.Holes.RemoveAt(index);
            SelectedHole = index < Operation.Holes.Count
                ? Operation.Holes[index]
                : Operation.Holes.LastOrDefault();
        }

        private bool CanMoveSelectedHoleUp()
            => SelectedHole != null && Operation != null && Operation.Holes.IndexOf(SelectedHole) > 0;

        private bool CanMoveSelectedHoleDown()
        {
            if (SelectedHole == null || Operation == null) return false;
            var index = Operation.Holes.IndexOf(SelectedHole);
            return index >= 0 && index < Operation.Holes.Count - 1;
        }

        private void MoveSelectedHoleUp()
        {
            if (Operation == null || SelectedHole == null || !CanMoveSelectedHoleUp()) return;

            var index = Operation.Holes.IndexOf(SelectedHole);
            Operation.Holes.Move(index, index - 1);
        }

        private void MoveSelectedHoleDown()
        {
            if (Operation == null || SelectedHole == null || !CanMoveSelectedHoleDown()) return;

            var index = Operation.Holes.IndexOf(SelectedHole);
            Operation.Holes.Move(index, index + 1);
        }
    }
}
