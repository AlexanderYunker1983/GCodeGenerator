using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class DrillRectOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillRectOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillRect");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по контуру прямоугольника" : title;

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

                // Initialize from metadata if available, otherwise from holes
                if (_operation.Metadata != null && _operation.Metadata.ContainsKey("StartX"))
                {
                    StartX = Convert.ToDouble(_operation.Metadata["StartX"]);
                    StartY = Convert.ToDouble(_operation.Metadata["StartY"]);
                    StartZ = Convert.ToDouble(_operation.Metadata["StartZ"]);
                    Distance = Convert.ToDouble(_operation.Metadata["Distance"]);
                    HoleCount = Convert.ToInt32(_operation.Metadata["HoleCount"]);
                    AngleDeg = Convert.ToDouble(_operation.Metadata["AngleDeg"]);
                    RowPitch = Convert.ToDouble(_operation.Metadata["RowPitch"]);
                    RowCount = Convert.ToInt32(_operation.Metadata["RowCount"]);
                    TotalDepth = Convert.ToDouble(_operation.Metadata["TotalDepth"]);
                    StepDepth = Convert.ToDouble(_operation.Metadata["StepDepth"]);
                    FeedZRapid = Convert.ToDouble(_operation.Metadata["FeedZRapid"]);
                    FeedZWork = Convert.ToDouble(_operation.Metadata["FeedZWork"]);
                    RetractHeight = Convert.ToDouble(_operation.Metadata["RetractHeight"]);
                }
                else if (_operation.Holes.Any())
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
                    // Default values for missing parameters
                    Distance = 10;
                    HoleCount = 3;
                    RowPitch = 10;
                    RowCount = 2;
                    AngleDeg = 0;
                }
                else
                {
                    StartX = 0;
                    StartY = 0;
                    StartZ = 0;
                    Distance = 10;
                    HoleCount = 3;
                    RowPitch = 10;
                    RowCount = 2;
                    AngleDeg = 0;
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

        private int _holeCount = 4;
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
                _rowCount = Math.Max(2, value);
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

            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.SafeZBetweenHoles = SafeZBetweenHoles;
            _operation.Decimals = Decimals;

            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (HoleCount <= 1 || Distance == 0 || RowCount <= 1)
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
                    // Skip interior points; keep only outer rectangle contour.
                    var isBorderRow = row == 0 || row == RowCount - 1;
                    var isBorderCol = col == 0 || col == HoleCount - 1;
                    if (!(isBorderRow || isBorderCol))
                        continue;

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


