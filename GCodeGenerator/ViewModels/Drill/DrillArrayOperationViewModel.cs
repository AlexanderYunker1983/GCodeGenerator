using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillArrayOperationViewModel : CloseableViewModel, IHasDisplayName, IDrillDialogViewModel
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillArrayOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillArray");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по массиву" : title;

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
                StartX = _operation.StartX;
                StartY = _operation.StartY;
                StartZ = _operation.StartZ;
                Distance = _operation.Distance;
                HoleCount = _operation.HoleCount;
                AngleDeg = _operation.AngleDeg;
                RowPitch = _operation.RowPitch;
                RowCount = _operation.RowCount;
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
            _operation.DrillMode = DrillMode.Array;
            _operation.StartX = StartX;
            _operation.StartY = StartY;
            _operation.StartZ = StartZ;
            _operation.Distance = Distance;
            _operation.HoleCount = HoleCount;
            _operation.AngleDeg = AngleDeg;
            _operation.RowPitch = RowPitch;
            _operation.RowCount = RowCount;
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
            // \u041f\u0443\u043d\u043a\u0442 7.1 \u043f\u043b\u0430\u043d\u0430: OnClosed \u0432\u044b\u0437\u044b\u0432\u0430\u0435\u0442\u0441\u044f IDialogService \u043d\u0430 UI-\u043f\u043e\u0442\u043e\u043a\u0435
            // \u043f\u043e\u0441\u043b\u0435 \u0437\u0430\u043a\u0440\u044b\u0442\u0438\u044f \u043c\u043e\u0434\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u043e\u043a\u043d\u0430 (WpfDialogService.ShowDialog) \u2014
            // \u043f\u0435\u0440\u0435\u0445\u043e\u0434 \u0447\u0435\u0440\u0435\u0437 Dispatcher \u043d\u0435 \u043d\u0443\u0436\u0435\u043d, \u0432\u044b\u0437\u043e\u0432 \u043f\u0440\u044f\u043c\u043e\u0439.
            MainViewModel?.RemoveOperation(_operation);
        }

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (HoleCount <= 0 || Distance == 0 || RowCount <= 0)
                return;

            var angleRad = AngleDeg * Math.PI / 180.0;
            var dx = Distance * Math.Cos(angleRad);
            var dy = Distance * Math.Sin(angleRad);

            // Perpendicular direction for rows (90 degrees counter-clockwise)
            var px = -Math.Sin(angleRad) * RowPitch;
            var py =  Math.Cos(angleRad) * RowPitch;

            for (int row = 0; row < RowCount; row++)
            {
                for (int col = 0; col < HoleCount; col++)
                {
                    var hole = new DrillHole
                    {
                        X = StartX + dx * col + px * row,
                        Y = StartY + dy * col + py * row,
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
}


