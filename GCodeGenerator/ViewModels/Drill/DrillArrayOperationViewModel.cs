using System;
using System.Collections.ObjectModel;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillArrayOperationViewModel : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillArrayOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("AddDrillArray") ?? "AddDrillArray";

            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        protected override void LoadFromOperation(DrillPointsOperation operation)
        {
            // Читаем типизированные свойства (пункт 3.3 плана): для новой
            // операции это дефолты модели, для загруженной — значения,
            // мигрированные из Metadata (пункт 3.2).
            StartX = operation.StartX;
            StartY = operation.StartY;
            StartZ = operation.StartZ;
            Distance = operation.Distance;
            HoleCount = operation.HoleCount;
            AngleDeg = operation.AngleDeg;
            RowPitch = operation.RowPitch;
            RowCount = operation.RowCount;
            TotalDepth = operation.TotalDepth;
            StepDepth = operation.StepDepth;
            FeedZRapid = operation.FeedZRapid;
            FeedZWork = operation.FeedZWork;
            RetractHeight = operation.RetractHeight;

            FeedXYRapid = operation.FeedXYRapid;
            FeedXYWork = operation.FeedXYWork;
            SafeZBetweenHoles = operation.SafeZBetweenHoles;
            Decimals = operation.Decimals;

            RebuildHoles();
        }

        public ObservableCollection<DrillHole> PreviewHoles { get; }

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

        private double _startX;
        public double StartX
        {
            get => _startX;
            set
            {
                if (value.Equals(_startX)) return;
                _startX = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _startY;
        public double StartY
        {
            get => _startY;
            set
            {
                if (value.Equals(_startY)) return;
                _startY = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _startZ;
        public double StartZ
        {
            get => _startZ;
            set
            {
                if (value.Equals(_startZ)) return;
                _startZ = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _distance;
        public double Distance
        {
            get => _distance;
            set
            {
                if (value.Equals(_distance)) return;
                _distance = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _holeCount = 3;
        public int HoleCount
        {
            get => _holeCount;
            set
            {
                if (value == _holeCount) return;
                _holeCount = Math.Max(1, value);
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _rowPitch;
        public double RowPitch
        {
            get => _rowPitch;
            set
            {
                if (value.Equals(_rowPitch)) return;
                _rowPitch = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _rowCount = 2;
        public int RowCount
        {
            get => _rowCount;
            set
            {
                if (value == _rowCount) return;
                _rowCount = Math.Max(1, value);
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _angleDeg;
        public double AngleDeg
        {
            get => _angleDeg;
            set
            {
                if (value.Equals(_angleDeg)) return;
                _angleDeg = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _totalDepth;
        public double TotalDepth
        {
            get => _totalDepth;
            set
            {
                if (value.Equals(_totalDepth)) return;
                _totalDepth = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _stepDepth;
        public double StepDepth
        {
            get => _stepDepth;
            set
            {
                if (value.Equals(_stepDepth)) return;
                _stepDepth = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _feedXYRapid = 1000;
        public double FeedXYRapid
        {
            get => _feedXYRapid;
            set
            {
                if (value.Equals(_feedXYRapid)) return;
                _feedXYRapid = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _feedXYWork = 300;
        public double FeedXYWork
        {
            get => _feedXYWork;
            set
            {
                if (value.Equals(_feedXYWork)) return;
                _feedXYWork = value;
                OnPropertyChanged();
            }
        }

        private double _feedZRapid = 500;
        public double FeedZRapid
        {
            get => _feedZRapid;
            set
            {
                if (value.Equals(_feedZRapid)) return;
                _feedZRapid = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _feedZWork = 200;
        public double FeedZWork
        {
            get => _feedZWork;
            set
            {
                if (value.Equals(_feedZWork)) return;
                _feedZWork = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _safeZBetweenHoles = 1;
        public double SafeZBetweenHoles
        {
            get => _safeZBetweenHoles;
            set
            {
                if (value.Equals(_safeZBetweenHoles)) return;
                _safeZBetweenHoles = value;
                OnPropertyChanged();
            }
        }

        private double _retractHeight = 0.3;
        public double RetractHeight
        {
            get => _retractHeight;
            set
            {
                if (value.Equals(_retractHeight)) return;
                _retractHeight = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _decimals = 3;
        public int Decimals
        {
            get => _decimals;
            set
            {
                if (value == _decimals) return;
                _decimals = value;
                OnPropertyChanged();
            }
        }

        protected override void ApplyToOperation()
        {
            ApplyPatternParameters(Operation);

            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        /// <summary>
        /// Переносит параметры шаблона из диалога в операцию. Используется и при
        /// сохранении, и при предварительном расчёте отверстий, поэтому диалог
        /// и файл проекта описывают шаблон одинаково.
        /// </summary>
        private void ApplyPatternParameters(DrillPointsOperation target)
        {
            target.FeedXYRapid = FeedXYRapid;
            target.FeedXYWork = FeedXYWork;
            target.SafeZBetweenHoles = SafeZBetweenHoles;
            target.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            target.DrillMode = DrillMode.Array;
            target.StartX = StartX;
            target.StartY = StartY;
            target.StartZ = StartZ;
            target.Distance = Distance;
            target.HoleCount = HoleCount;
            target.AngleDeg = AngleDeg;
            target.RowPitch = RowPitch;
            target.RowCount = RowCount;
            target.TotalDepth = TotalDepth;
            target.StepDepth = StepDepth;
            target.FeedZRapid = FeedZRapid;
            target.FeedZWork = FeedZWork;
            target.RetractHeight = RetractHeight;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // массив без отверстий (HoleCount<=0, Distance==0 или RowCount<=0) не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;

        /// <summary>
        /// Пересчитывает отверстия шаблона для предпросмотра. Расчёт выполняет
        /// ядро (<see cref="DrillPatternBuilder"/>), поэтому диалог и сохранённая
        /// операция описывают одни и те же отверстия.
        /// </summary>
        private void RebuildHoles()
        {
            PreviewHoles.Clear();

            var pattern = new DrillPointsOperation();
            ApplyPatternParameters(pattern);
            foreach (var hole in DrillPatternBuilder.Build(pattern))
                PreviewHoles.Add(hole);
        }
    }
}


