using System;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketRectangleOperationViewModel : OperationEditorViewModelBase<PocketRectangleOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public PocketRectangleOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("PocketRectangleName");
            DisplayName = string.IsNullOrEmpty(title) ? "Карман прямоугольный" : title;
        }

        protected override void LoadFromOperation(PocketRectangleOperation operation)
        {
            Width = operation.Width;
            Height = operation.Height;
            RotationAngle = operation.RotationAngle;
            TotalDepth = operation.TotalDepth;
            StepDepth = operation.StepDepth;
            ToolDiameter = operation.ToolDiameter;
            ContourHeight = operation.ContourHeight;
            FeedXYRapid = operation.FeedXYRapid;
            FeedXYWork = operation.FeedXYWork;
            FeedZRapid = operation.FeedZRapid;
            FeedZWork = operation.FeedZWork;
            SafeZHeight = operation.SafeZHeight;
            RetractHeight = operation.RetractHeight;
            ReferencePointX = operation.ReferencePointX;
            ReferencePointY = operation.ReferencePointY;
            ReferencePointType = operation.ReferencePointType;
            Direction = operation.Direction;
            PocketStrategy = operation.PocketStrategy;
            StepPercentOfTool = operation.StepPercentOfTool;
            Decimals = operation.Decimals;
            LineAngleDeg = operation.LineAngleDeg;
            WallTaperAngleDeg = Math.Max(0, operation.WallTaperAngleDeg);

            IsRoughingEnabled = operation.IsRoughingEnabled;
            IsFinishingEnabled = operation.IsFinishingEnabled;
            FinishAllowance = operation.FinishAllowance;
            FinishingMode = operation.FinishingMode;
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

        private PocketStrategy _pocketStrategy = PocketStrategy.Spiral;
        public PocketStrategy PocketStrategy
        {
            get => _pocketStrategy;
            set
            {
                if (value == _pocketStrategy) return;
                _pocketStrategy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLinesStrategy));
                OnPropertyChanged(nameof(IsLinesOrZigZagStrategy));
            }
        }

        public bool IsLinesStrategy => PocketStrategy == PocketStrategy.Lines;
        public bool IsLinesOrZigZagStrategy => PocketStrategy == PocketStrategy.Lines || PocketStrategy == PocketStrategy.ZigZag;

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

        private double _stepPercent = 40.0;
        public double StepPercentOfTool
        {
            get => _stepPercent;
            set
            {
                if (value.Equals(_stepPercent)) return;
                _stepPercent = value;
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

        private double _lineAngleDeg = 0.0;
        public double LineAngleDeg
        {
            get => _lineAngleDeg;
            set
            {
                if (value.Equals(_lineAngleDeg)) return;
                _lineAngleDeg = value;
                OnPropertyChanged();
            }
        }

        private double _wallTaperAngleDeg = 0.0;
        public double WallTaperAngleDeg
        {
            get => _wallTaperAngleDeg;
            set
            {
                // Ограничиваем угол диапазоном [0; 90)
                var v = Math.Max(0, Math.Min(89.999999, value));
                if (v.Equals(_wallTaperAngleDeg)) return;
                _wallTaperAngleDeg = v;
                OnPropertyChanged();
            }
        }

        private bool _isRoughingEnabled;
        public bool IsRoughingEnabled
        {
            get => _isRoughingEnabled;
            set
            {
                if (value == _isRoughingEnabled) return;
                _isRoughingEnabled = value;
                if (_isRoughingEnabled)
                {
                    // Взаимоисключающее включение
                    _isFinishingEnabled = false;
                    OnPropertyChanged(nameof(IsFinishingEnabled));
                }
                OnPropertyChanged();
            }
        }

        private bool _isFinishingEnabled;
        public bool IsFinishingEnabled
        {
            get => _isFinishingEnabled;
            set
            {
                if (value == _isFinishingEnabled) return;
                _isFinishingEnabled = value;
                if (_isFinishingEnabled)
                {
                    _isRoughingEnabled = false;
                    OnPropertyChanged(nameof(IsRoughingEnabled));
                }
                OnPropertyChanged();
            }
        }

        private double _finishAllowance;
        public double FinishAllowance
        {
            get => _finishAllowance;
            set
            {
                if (value.Equals(_finishAllowance)) return;
                _finishAllowance = value;
                OnPropertyChanged();
            }
        }

        private PocketFinishingMode _finishingMode = PocketFinishingMode.All;
        public PocketFinishingMode FinishingMode
        {
            get => _finishingMode;
            set
            {
                if (value == _finishingMode) return;
                _finishingMode = value;
                OnPropertyChanged();
            }
        }


        protected override void ApplyToOperation()
        {
            Operation.Direction = Direction;
            Operation.PocketStrategy = PocketStrategy;
            Operation.Width = Width;
            Operation.Height = Height;
            Operation.RotationAngle = RotationAngle;
            Operation.TotalDepth = TotalDepth;
            Operation.StepDepth = StepDepth;
            Operation.ToolDiameter = ToolDiameter;
            Operation.ContourHeight = ContourHeight;
            Operation.FeedXYRapid = FeedXYRapid;
            Operation.FeedXYWork = FeedXYWork;
            Operation.FeedZRapid = FeedZRapid;
            Operation.FeedZWork = FeedZWork;
            Operation.SafeZHeight = SafeZHeight;
            Operation.RetractHeight = RetractHeight;
            Operation.ReferencePointX = ReferencePointX;
            Operation.ReferencePointY = ReferencePointY;
            Operation.ReferencePointType = ReferencePointType;
            Operation.StepPercentOfTool = StepPercentOfTool;
            Operation.Decimals = Decimals;
            Operation.LineAngleDeg = LineAngleDeg;
            Operation.WallTaperAngleDeg = WallTaperAngleDeg;
            Operation.IsRoughingEnabled = IsRoughingEnabled;
            Operation.IsFinishingEnabled = IsFinishingEnabled;
            Operation.FinishAllowance = FinishAllowance;
            Operation.FinishingMode = FinishingMode;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => Width > 0 && Height > 0 && ToolDiameter > 0 && StepPercentOfTool > 0;
    }
}


