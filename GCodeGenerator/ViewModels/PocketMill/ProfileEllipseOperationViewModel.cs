using System;
using System.Windows.Threading;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileEllipseOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfileEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            DisplayName = _localizationManager?.GetString("ProfileEllipseName") ?? "";
        }

        public ProfileMillingOperationsViewModel ProfileMillingOperationsViewModel { get; set; }

        private ProfileEllipseOperation _operation;

        public ProfileEllipseOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                // Читаем только типизированные свойства (пункт 3.5 плана):
                // легаси-Metadata мигрируется в свойства при загрузке (пункт 3.2).
                ToolPathMode = _operation.ToolPathMode;
                Direction = _operation.Direction;
                CenterX = _operation.CenterX;
                CenterY = _operation.CenterY;
                RadiusX = _operation.RadiusX;
                RadiusY = _operation.RadiusY;
                RotationAngle = _operation.RotationAngle;
                MaxSegmentLength = _operation.MaxSegmentLength;
                TotalDepth = _operation.TotalDepth;
                StepDepth = _operation.StepDepth;
                ToolDiameter = _operation.ToolDiameter;
                ContourHeight = _operation.ContourHeight;
                FeedXYRapid = _operation.FeedXYRapid;
                FeedXYWork = _operation.FeedXYWork;
                FeedZRapid = _operation.FeedZRapid;
                FeedZWork = _operation.FeedZWork;
                SafeZHeight = _operation.SafeZHeight;
                RetractHeight = _operation.RetractHeight;
                EntryMode = _operation.EntryMode;
                EntryAngle = _operation.EntryAngle;
                SafeDistanceBetweenPasses = _operation.SafeDistanceBetweenPasses;
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

        private ToolPathMode _toolPathMode = ToolPathMode.OnLine;
        public ToolPathMode ToolPathMode
        {
            get => _toolPathMode;
            set
            {
                if (value == _toolPathMode) return;
                _toolPathMode = value;
                OnPropertyChanged();
            }
        }

        private MillingDirection _direction = MillingDirection.Clockwise;
        public MillingDirection Direction
        {
            get => _direction;
            set
            {
                if (value == _direction) return;
                _direction = value;
                OnPropertyChanged();
            }
        }

        private double _centerX = 0.0;
        public double CenterX
        {
            get => _centerX;
            set
            {
                if (value.Equals(_centerX)) return;
                _centerX = value;
                OnPropertyChanged();
            }
        }

        private double _centerY = 0.0;
        public double CenterY
        {
            get => _centerY;
            set
            {
                if (value.Equals(_centerY)) return;
                _centerY = value;
                OnPropertyChanged();
            }
        }

        private double _radiusX = 10.0;
        public double RadiusX
        {
            get => _radiusX;
            set
            {
                if (value.Equals(_radiusX)) return;
                _radiusX = value;
                OnPropertyChanged();
            }
        }

        private double _radiusY = 10.0;
        public double RadiusY
        {
            get => _radiusY;
            set
            {
                if (value.Equals(_radiusY)) return;
                _radiusY = value;
                OnPropertyChanged();
            }
        }

        private double _rotationAngle = 0.0;
        public double RotationAngle
        {
            get => _rotationAngle;
            set
            {
                if (value.Equals(_rotationAngle)) return;
                _rotationAngle = value;
                OnPropertyChanged();
            }
        }

        private double _maxSegmentLength = 0.5;
        public double MaxSegmentLength
        {
            get => _maxSegmentLength;
            set
            {
                if (value.Equals(_maxSegmentLength)) return;
                _maxSegmentLength = value;
                OnPropertyChanged();
            }
        }

        private double _totalDepth = 2.0;
        public double TotalDepth
        {
            get => _totalDepth;
            set
            {
                if (value.Equals(_totalDepth)) return;
                _totalDepth = value;
                OnPropertyChanged();
            }
        }

        private double _stepDepth = 1.0;
        public double StepDepth
        {
            get => _stepDepth;
            set
            {
                if (value.Equals(_stepDepth)) return;
                _stepDepth = value;
                OnPropertyChanged();
            }
        }

        private double _toolDiameter = 3.0;
        public double ToolDiameter
        {
            get => _toolDiameter;
            set
            {
                if (value.Equals(_toolDiameter)) return;
                _toolDiameter = value;
                OnPropertyChanged();
            }
        }

        private double _contourHeight = 0.0;
        public double ContourHeight
        {
            get => _contourHeight;
            set
            {
                if (value.Equals(_contourHeight)) return;
                _contourHeight = value;
                OnPropertyChanged();
            }
        }

        private double _feedXYRapid = 1000.0;
        public double FeedXYRapid
        {
            get => _feedXYRapid;
            set
            {
                if (value.Equals(_feedXYRapid)) return;
                _feedXYRapid = value;
                OnPropertyChanged();
            }
        }

        private double _feedXYWork = 300.0;
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

        private double _feedZRapid = 500.0;
        public double FeedZRapid
        {
            get => _feedZRapid;
            set
            {
                if (value.Equals(_feedZRapid)) return;
                _feedZRapid = value;
                OnPropertyChanged();
            }
        }

        private double _feedZWork = 200.0;
        public double FeedZWork
        {
            get => _feedZWork;
            set
            {
                if (value.Equals(_feedZWork)) return;
                _feedZWork = value;
                OnPropertyChanged();
            }
        }

        private double _safeZHeight = 1.0;
        public double SafeZHeight
        {
            get => _safeZHeight;
            set
            {
                if (value.Equals(_safeZHeight)) return;
                _safeZHeight = value;
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
            }
        }

        private EntryMode _entryMode = EntryMode.Vertical;
        public EntryMode EntryMode
        {
            get => _entryMode;
            set
            {
                if (value == _entryMode) return;
                _entryMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAngledEntry));
            }
        }

        public bool IsAngledEntry => EntryMode == EntryMode.Angled;

        private double _entryAngle = 5.0;
        public double EntryAngle
        {
            get => _entryAngle;
            set
            {
                if (value.Equals(_entryAngle)) return;
                _entryAngle = value;
                OnPropertyChanged();
            }
        }

        private double _safeDistanceBetweenPasses = 1.0;
        public double SafeDistanceBetweenPasses
        {
            get => _safeDistanceBetweenPasses;
            set
            {
                if (value.Equals(_safeDistanceBetweenPasses)) return;
                _safeDistanceBetweenPasses = value;
                OnPropertyChanged();
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

            // Remove operation if no valid parameters
            if (RadiusX <= 0 || RadiusY <= 0 || ToolDiameter <= 0)
            {
                RemoveOperationFromMain();
                return;
            }

            // Save to operation
            _operation.ToolPathMode = ToolPathMode;
            _operation.Direction = Direction;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.RadiusX = RadiusX;
            _operation.RadiusY = RadiusY;
            _operation.RotationAngle = RotationAngle;
            _operation.MaxSegmentLength = MaxSegmentLength;
            _operation.TotalDepth = TotalDepth;
            _operation.StepDepth = StepDepth;
            _operation.ToolDiameter = ToolDiameter;
            _operation.ContourHeight = ContourHeight;
            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.FeedZRapid = FeedZRapid;
            _operation.FeedZWork = FeedZWork;
            _operation.SafeZHeight = SafeZHeight;
            _operation.RetractHeight = RetractHeight;
            _operation.EntryMode = EntryMode;
            _operation.EntryAngle = EntryAngle;
            _operation.SafeDistanceBetweenPasses = SafeDistanceBetweenPasses;
            _operation.Decimals = Decimals;
        }

        private void RemoveOperationFromMain()
        {
            if (ProfileMillingOperationsViewModel != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    ProfileMillingOperationsViewModel.RemoveOperation(_operation);
                }
                else
                {
                    dispatcher.Invoke(() => ProfileMillingOperationsViewModel.RemoveOperation(_operation));
                }
            }
        }
    }
}

