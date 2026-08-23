using System;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileCircleOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfileCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("ProfileCircleName");
            DisplayName = string.IsNullOrEmpty(title) ? "Контур окружности" : title;
        }

        public ProfileMillingOperationsViewModel ProfileMillingOperationsViewModel { get; set; }

        private ProfileCircleOperation _operation;

        public ProfileCircleOperation Operation
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
                Radius = _operation.Radius;
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

        private double _radius = 10.0;
        public double Radius
        {
            get => _radius;
            set
            {
                if (value.Equals(_radius)) return;
                _radius = value;
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
            if (Radius <= 0 || ToolDiameter <= 0)
            {
                RemoveOperationFromMain();
                return;
            }

            // Save to operation
            _operation.ToolPathMode = ToolPathMode;
            _operation.Direction = Direction;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.Radius = Radius;
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
            // \u041f\u0443\u043d\u043a\u0442 7.1 \u043f\u043b\u0430\u043d\u0430: OnClosed \u0432\u044b\u0437\u044b\u0432\u0430\u0435\u0442\u0441\u044f IDialogService \u043d\u0430 UI-\u043f\u043e\u0442\u043e\u043a\u0435
            // \u043f\u043e\u0441\u043b\u0435 \u0437\u0430\u043a\u0440\u044b\u0442\u0438\u044f \u043c\u043e\u0434\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u043e\u043a\u043d\u0430 (WpfDialogService.ShowDialog) \u2014
            // \u043f\u0435\u0440\u0435\u0445\u043e\u0434 \u0447\u0435\u0440\u0435\u0437 Dispatcher \u043d\u0435 \u043d\u0443\u0436\u0435\u043d, \u0432\u044b\u0437\u043e\u0432 \u043f\u0440\u044f\u043c\u043e\u0439.
            ProfileMillingOperationsViewModel?.RemoveOperation(_operation);
        }
    }
}

