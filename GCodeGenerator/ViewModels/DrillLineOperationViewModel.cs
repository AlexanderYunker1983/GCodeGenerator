using System;
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
    public class DrillLineOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillLineOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillLine");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по линии" : title;

            PreviewHoles = new ObservableCollection<DrillHole>();
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

                // Initialize from existing operation if it already has holes.
                if (_operation.Holes.Any())
                {
                    var first = _operation.Holes.First();
                    StartX = first.X;
                    StartY = first.Y;
                    StartZ = first.Z;
                    TotalDepth = first.TotalDepth;
                    StepDepth = first.StepDepth;
                    FeedZRapid = first.FeedZRapid;
                    FeedZWork = first.FeedZWork;
                    RetractHeight = first.RetractHeight;
                }
                else
                {
                    // Default values.
                    StartX = 0;
                    StartY = 0;
                    StartZ = 0;
                    TotalDepth = 2;
                    StepDepth = 1;
                    FeedZRapid = 500;
                    FeedZWork = 200;
                    RetractHeight = 5;
                }

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

        private double _safeZBetweenHoles = 5;
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

        private double _retractHeight = 5;
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

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

            // Save common parameters to operation.
            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.SafeZBetweenHoles = SafeZBetweenHoles;
            _operation.Decimals = Decimals;

            // Save generated holes.
            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }

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


