using System;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using System.Collections.ObjectModel;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfileRoundedRectangleOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfileRoundedRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("ProfileRoundedRectangleName");
            DisplayName = string.IsNullOrEmpty(title) ? "Контур скругленного прямоугольника" : title;
        }

        public ObservableCollection<OperationBase> Operations { get; set; }

        private ProfileRoundedRectangleOperation _operation;

        public ProfileRoundedRectangleOperation Operation
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
                Width = _operation.Width;
                Height = _operation.Height;
                RotationAngle = _operation.RotationAngle;
                RadiusTopLeft = _operation.RadiusTopLeft;
                RadiusTopRight = _operation.RadiusTopRight;
                RadiusBottomLeft = _operation.RadiusBottomLeft;
                RadiusBottomRight = _operation.RadiusBottomRight;
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
                ReferencePointX = _operation.ReferencePointX;
                ReferencePointY = _operation.ReferencePointY;
                ReferencePointType = _operation.ReferencePointType;
                EntryMode = _operation.EntryMode;
                EntryAngle = _operation.EntryAngle;
                SafeDistanceBetweenPasses = _operation.SafeDistanceBetweenPasses;
                Decimals = _operation.Decimals;
                MaxSegmentLength = _operation.MaxSegmentLength;
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

        private double _width = 10.0;
        public double Width
        {
            get => _width;
            set
            {
                if (value.Equals(_width)) return;
                _width = value;
                OnPropertyChanged();
            }
        }

        private double _height = 10.0;
        public double Height
        {
            get => _height;
            set
            {
                if (value.Equals(_height)) return;
                _height = value;
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

        private double _radiusTopLeft = 2.0;
        public double RadiusTopLeft
        {
            get => _radiusTopLeft;
            set
            {
                if (value.Equals(_radiusTopLeft)) return;
                _radiusTopLeft = value;
                OnPropertyChanged();
            }
        }

        private double _radiusTopRight = 2.0;
        public double RadiusTopRight
        {
            get => _radiusTopRight;
            set
            {
                if (value.Equals(_radiusTopRight)) return;
                _radiusTopRight = value;
                OnPropertyChanged();
            }
        }

        private double _radiusBottomLeft = 2.0;
        public double RadiusBottomLeft
        {
            get => _radiusBottomLeft;
            set
            {
                if (value.Equals(_radiusBottomLeft)) return;
                _radiusBottomLeft = value;
                OnPropertyChanged();
            }
        }

        private double _radiusBottomRight = 2.0;
        public double RadiusBottomRight
        {
            get => _radiusBottomRight;
            set
            {
                if (value.Equals(_radiusBottomRight)) return;
                _radiusBottomRight = value;
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

        private double _referencePointX = 0.0;
        public double ReferencePointX
        {
            get => _referencePointX;
            set
            {
                if (value.Equals(_referencePointX)) return;
                _referencePointX = value;
                OnPropertyChanged();
            }
        }

        private double _referencePointY = 0.0;
        public double ReferencePointY
        {
            get => _referencePointY;
            set
            {
                if (value.Equals(_referencePointY)) return;
                _referencePointY = value;
                OnPropertyChanged();
            }
        }

        private ReferencePointType _referencePointType = ReferencePointType.Center;
        public ReferencePointType ReferencePointType
        {
            get => _referencePointType;
            set
            {
                if (value == _referencePointType) return;
                _referencePointType = value;
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

        public override void OnClosed()
        {
            base.OnClosed();
            if (_operation == null) return;

            if (Width <= 0 || Height <= 0 || ToolDiameter <= 0)
            {
                RemoveOperationFromMain();
                return;
            }

            _operation.ToolPathMode = ToolPathMode;
            _operation.Direction = Direction;
            _operation.Width = Width;
            _operation.Height = Height;
            _operation.RotationAngle = RotationAngle;
            _operation.RadiusTopLeft = RadiusTopLeft;
            _operation.RadiusTopRight = RadiusTopRight;
            _operation.RadiusBottomLeft = RadiusBottomLeft;
            _operation.RadiusBottomRight = RadiusBottomRight;
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
            _operation.ReferencePointX = ReferencePointX;
            _operation.ReferencePointY = ReferencePointY;
            _operation.ReferencePointType = ReferencePointType;
            _operation.EntryMode = EntryMode;
            _operation.EntryAngle = EntryAngle;
            _operation.SafeDistanceBetweenPasses = SafeDistanceBetweenPasses;
            _operation.Decimals = Decimals;
            _operation.MaxSegmentLength = MaxSegmentLength;
        }

        private void RemoveOperationFromMain()
        {
            // Пункт 7.2 плана: единая коллекция операций (MainViewModel.AllOperations) —
            // прямое удаление; MainViewModel реагирует на CollectionChanged
            // и на PropertyChanged операции.
            Operations?.Remove(_operation);
        }
    }
}


