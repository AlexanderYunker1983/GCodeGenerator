using System;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketCircleOperationViewModel : OperationEditorViewModelBase<PocketCircleOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public PocketCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("PocketCircleName") ?? "PocketCircleName";
        }

        protected override void LoadFromOperation(PocketCircleOperation operation)
        {
            Direction = operation.Direction;
            PocketStrategy = operation.PocketStrategy;
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Radius = operation.Radius;
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
            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.Radius = Radius;
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
        protected override bool IsValid() => Radius > 0 && ToolDiameter > 0 && StepPercentOfTool > 0;
    }
}


