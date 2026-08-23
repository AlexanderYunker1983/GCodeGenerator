using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillArcOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillArcOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillArc");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по дуге" : title;

            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        public DrillOperationsViewModel MainViewModel { get; set; }

        private DrillPointsOperation _operation;

        public DrillPointsOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                // Читаем типизированные свойства (пункт 3.3 плана): для новой
                // операции это дефолты модели, для загруженной — значения,
                // мигрированные из Metadata (пункт 3.2).
                CenterX = _operation.CenterX;
                CenterY = _operation.CenterY;
                Z = _operation.Z;
                Radius = _operation.Radius;
                HoleCount = _operation.HoleCount;
                StartAngleDeg = _operation.StartAngleDeg;
                EndAngleDeg = _operation.EndAngleDeg;
                TotalDepth = _operation.TotalDepth;
                StepDepth = _operation.StepDepth;
                FeedZRapid = _operation.FeedZRapid;
                FeedZWork = _operation.FeedZWork;
                RetractHeight = _operation.RetractHeight;

                FeedXYRapid = _operation.FeedXYRapid;
                FeedXYWork = _operation.FeedXYWork;
                SafeZBetweenHoles = _operation.SafeZBetweenHoles;
                Decimals = _operation.Decimals;

                RebuildHoles();
            }
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

        private double _centerX;
        public double CenterX
        {
            get => _centerX;
            set
            {
                if (value.Equals(_centerX)) return;
                _centerX = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _centerY;
        public double CenterY
        {
            get => _centerY;
            set
            {
                if (value.Equals(_centerY)) return;
                _centerY = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _z;
        public double Z
        {
            get => _z;
            set
            {
                if (value.Equals(_z)) return;
                _z = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _radius;
        public double Radius
        {
            get => _radius;
            set
            {
                if (value.Equals(_radius)) return;
                _radius = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _holeCount = 2;
        public int HoleCount
        {
            get => _holeCount;
            set
            {
                if (value == _holeCount) return;
                _holeCount = Math.Max(2, value);
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _startAngleDeg;
        public double StartAngleDeg
        {
            get => _startAngleDeg;
            set
            {
                if (value.Equals(_startAngleDeg)) return;
                _startAngleDeg = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _endAngleDeg = 90;
        public double EndAngleDeg
        {
            get => _endAngleDeg;
            set
            {
                if (value.Equals(_endAngleDeg)) return;
                _endAngleDeg = value;
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

        public override void OnClosed()
        {
            base.OnClosed();
            if (_operation == null) return;

            // Remove operation if no holes were created
            if (PreviewHoles.Count == 0)
            {
                RemoveOperationFromMain();
                return;
            }

            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.SafeZBetweenHoles = SafeZBetweenHoles;
            _operation.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            _operation.DrillMode = DrillMode.Arc;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.Z = Z;
            _operation.Radius = Radius;
            _operation.HoleCount = HoleCount;
            _operation.StartAngleDeg = StartAngleDeg;
            _operation.EndAngleDeg = EndAngleDeg;
            _operation.TotalDepth = TotalDepth;
            _operation.StepDepth = StepDepth;
            _operation.FeedZRapid = FeedZRapid;
            _operation.FeedZWork = FeedZWork;
            _operation.RetractHeight = RetractHeight;

            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }

        private void RemoveOperationFromMain()
        {
            if (MainViewModel != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    MainViewModel.RemoveOperation(_operation);
                }
                else
                {
                    dispatcher.Invoke(() => MainViewModel.RemoveOperation(_operation));
                }
            }
        }

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (HoleCount < 1 || Radius == 0)
                return;

            var startRad = StartAngleDeg * Math.PI / 180.0;
            var endRad = EndAngleDeg * Math.PI / 180.0;
            
            // Calculate the arc span
            var arcSpan = endRad - startRad;
            // Normalize to [0, 2π] range for arcs that cross 0 degrees
            while (arcSpan < 0) arcSpan += 2 * Math.PI;
            while (arcSpan > 2 * Math.PI) arcSpan -= 2 * Math.PI;
            
            // If arcSpan is 0 or very small, make it 2π (full circle)
            if (arcSpan < 0.001)
                arcSpan = 2 * Math.PI;
            
            // Calculate step between holes
            var stepRad = HoleCount > 1 ? arcSpan / (HoleCount - 1) : 0;

            for (int i = 0; i < HoleCount; i++)
            {
                var angle = startRad + stepRad * i;
                var x = CenterX + Radius * Math.Cos(angle);
                var y = CenterY + Radius * Math.Sin(angle);

                var hole = new DrillHole
                {
                    X = x,
                    Y = y,
                    Z = Z,
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

