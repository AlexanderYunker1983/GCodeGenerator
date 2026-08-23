using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillLineOperationViewModel : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName, IDrillDialogViewModel
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillLineOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillLine");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по линии" : title;

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
            // Save common parameters to operation.
            Operation.FeedXYRapid = FeedXYRapid;
            Operation.FeedXYWork = FeedXYWork;
            Operation.SafeZBetweenHoles = SafeZBetweenHoles;
            Operation.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            Operation.DrillMode = DrillMode.Line;
            Operation.StartX = StartX;
            Operation.StartY = StartY;
            Operation.StartZ = StartZ;
            Operation.Distance = Distance;
            Operation.HoleCount = HoleCount;
            Operation.AngleDeg = AngleDeg;
            Operation.TotalDepth = TotalDepth;
            Operation.StepDepth = StepDepth;
            Operation.FeedZRapid = FeedZRapid;
            Operation.FeedZWork = FeedZWork;
            Operation.RetractHeight = RetractHeight;

            // Save generated holes.
            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // линия без отверстий (HoleCount<=0 или Distance==0) не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (HoleCount <= 0 || Distance == 0)
                return;

            var angleRad = AngleDeg * Math.PI / 180.0;
            var dx = Distance * Math.Cos(angleRad);
            var dy = Distance * Math.Sin(angleRad);

            for (int i = 0; i < HoleCount; i++)
            {
                var hole = new DrillHole
                {
                    X = StartX + dx * i,
                    Y = StartY + dy * i,
                    Z = StartZ,
                    TotalDepth = TotalDepth,
                    StepDepth = StepDepth,
                    FeedZRapid = FeedZRapid,
                    FeedZWork = FeedZWork,
                    RetractHeight = RetractHeight
                };
                PreviewHoles.Add(hole);
            }
        }
    }
}


