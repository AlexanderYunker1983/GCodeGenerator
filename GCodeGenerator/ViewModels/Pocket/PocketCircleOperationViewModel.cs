using System;
using System.Windows.Threading;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketCircleOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public PocketCircleOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("PocketCircleName");
            DisplayName = string.IsNullOrEmpty(title) ? "Карман круглый" : title;
        }

        public PocketOperationsViewModel PocketOperationsViewModel { get; set; }

        private PocketCircleOperation _operation;

        public PocketCircleOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                if (_operation.Metadata != null && _operation.Metadata.ContainsKey("Radius"))
                {
                    Direction = (MillingDirection)_operation.Metadata["Direction"];
                    if (_operation.Metadata.ContainsKey("PocketStrategy"))
                        PocketStrategy = (PocketStrategy)_operation.Metadata["PocketStrategy"];
                    else
                        PocketStrategy = _operation.PocketStrategy;
                    CenterX = Convert.ToDouble(_operation.Metadata["CenterX"]);
                    CenterY = Convert.ToDouble(_operation.Metadata["CenterY"]);
                    Radius = Convert.ToDouble(_operation.Metadata["Radius"]);
                    TotalDepth = Convert.ToDouble(_operation.Metadata["TotalDepth"]);
                    StepDepth = Convert.ToDouble(_operation.Metadata["StepDepth"]);
                    ToolDiameter = Convert.ToDouble(_operation.Metadata["ToolDiameter"]);
                    ContourHeight = Convert.ToDouble(_operation.Metadata["ContourHeight"]);
                    FeedXYRapid = Convert.ToDouble(_operation.Metadata["FeedXYRapid"]);
                    FeedXYWork = Convert.ToDouble(_operation.Metadata["FeedXYWork"]);
                    FeedZRapid = Convert.ToDouble(_operation.Metadata["FeedZRapid"]);
                    FeedZWork = Convert.ToDouble(_operation.Metadata["FeedZWork"]);
                    SafeZHeight = Convert.ToDouble(_operation.Metadata["SafeZHeight"]);
                    RetractHeight = Convert.ToDouble(_operation.Metadata["RetractHeight"]);
                    StepPercentOfTool = Convert.ToDouble(_operation.Metadata["StepPercentOfTool"]);
                    Decimals = Convert.ToInt32(_operation.Metadata["Decimals"]);
                    if (_operation.Metadata.ContainsKey("LineAngleDeg"))
                        LineAngleDeg = Convert.ToDouble(_operation.Metadata["LineAngleDeg"]);
                    if (_operation.Metadata.ContainsKey("WallTaperAngleDeg"))
                        WallTaperAngleDeg = Math.Max(0, Convert.ToDouble(_operation.Metadata["WallTaperAngleDeg"]));

                    if (_operation.Metadata.ContainsKey("IsRoughingEnabled"))
                        IsRoughingEnabled = Convert.ToBoolean(_operation.Metadata["IsRoughingEnabled"]);
                    else
                        IsRoughingEnabled = _operation.IsRoughingEnabled;

                    if (_operation.Metadata.ContainsKey("IsFinishingEnabled"))
                        IsFinishingEnabled = Convert.ToBoolean(_operation.Metadata["IsFinishingEnabled"]);
                    else
                        IsFinishingEnabled = _operation.IsFinishingEnabled;

                    if (_operation.Metadata.ContainsKey("FinishAllowance"))
                        FinishAllowance = Convert.ToDouble(_operation.Metadata["FinishAllowance"]);
                    else
                        FinishAllowance = _operation.FinishAllowance;

                    if (_operation.Metadata.ContainsKey("FinishingMode"))
                        FinishingMode = (PocketFinishingMode)_operation.Metadata["FinishingMode"];
                    else
                        FinishingMode = _operation.FinishingMode;
                }
                else
                {
                    Direction = _operation.Direction;
                    PocketStrategy = _operation.PocketStrategy;
                    CenterX = _operation.CenterX;
                    CenterY = _operation.CenterY;
                    Radius = _operation.Radius;
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
                    StepPercentOfTool = _operation.StepPercentOfTool;
                    Decimals = _operation.Decimals;
                    LineAngleDeg = _operation.LineAngleDeg;
                    WallTaperAngleDeg = Math.Max(0, _operation.WallTaperAngleDeg);

                    IsRoughingEnabled = _operation.IsRoughingEnabled;
                    IsFinishingEnabled = _operation.IsFinishingEnabled;
                    FinishAllowance = _operation.FinishAllowance;
                    FinishingMode = _operation.FinishingMode;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("IsIslandMillingEnabled"))
                        IsIslandMillingEnabled = Convert.ToBoolean(_operation.Metadata["IsIslandMillingEnabled"]);
                    else
                        IsIslandMillingEnabled = _operation.IsIslandMillingEnabled;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("OuterBoundaryType"))
                        OuterBoundaryType = (OuterBoundaryType)_operation.Metadata["OuterBoundaryType"];
                    else
                        OuterBoundaryType = _operation.OuterBoundaryType;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("OuterBoundaryCenterX"))
                        OuterBoundaryCenterX = Convert.ToDouble(_operation.Metadata["OuterBoundaryCenterX"]);
                    else
                        OuterBoundaryCenterX = _operation.OuterBoundaryCenterX;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("OuterBoundaryCenterY"))
                        OuterBoundaryCenterY = Convert.ToDouble(_operation.Metadata["OuterBoundaryCenterY"]);
                    else
                        OuterBoundaryCenterY = _operation.OuterBoundaryCenterY;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("OuterBoundaryWidth"))
                        OuterBoundaryWidth = Convert.ToDouble(_operation.Metadata["OuterBoundaryWidth"]);
                    else
                        OuterBoundaryWidth = _operation.OuterBoundaryWidth;

                    if (_operation.Metadata != null && _operation.Metadata.ContainsKey("OuterBoundaryHeight"))
                        OuterBoundaryHeight = Convert.ToDouble(_operation.Metadata["OuterBoundaryHeight"]);
                    else
                        OuterBoundaryHeight = _operation.OuterBoundaryHeight;
                }
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
                var v = Math.Max(0, value);
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

        private bool _isIslandMillingEnabled = false;
        public bool IsIslandMillingEnabled
        {
            get => _isIslandMillingEnabled;
            set
            {
                if (value == _isIslandMillingEnabled) return;
                _isIslandMillingEnabled = value;
                OnPropertyChanged();
            }
        }

        private OuterBoundaryType _outerBoundaryType = OuterBoundaryType.Rectangle;
        public OuterBoundaryType OuterBoundaryType
        {
            get => _outerBoundaryType;
            set
            {
                if (value == _outerBoundaryType) return;
                _outerBoundaryType = value;
                OnPropertyChanged();
            }
        }

        private double _outerBoundaryCenterX = 0.0;
        public double OuterBoundaryCenterX
        {
            get => _outerBoundaryCenterX;
            set
            {
                if (value.Equals(_outerBoundaryCenterX)) return;
                _outerBoundaryCenterX = value;
                OnPropertyChanged();
            }
        }

        private double _outerBoundaryCenterY = 0.0;
        public double OuterBoundaryCenterY
        {
            get => _outerBoundaryCenterY;
            set
            {
                if (value.Equals(_outerBoundaryCenterY)) return;
                _outerBoundaryCenterY = value;
                OnPropertyChanged();
            }
        }

        private double _outerBoundaryWidth = 50.0;
        public double OuterBoundaryWidth
        {
            get => _outerBoundaryWidth;
            set
            {
                if (value.Equals(_outerBoundaryWidth)) return;
                _outerBoundaryWidth = value;
                OnPropertyChanged();
            }
        }

        private double _outerBoundaryHeight = 50.0;
        public double OuterBoundaryHeight
        {
            get => _outerBoundaryHeight;
            set
            {
                if (value.Equals(_outerBoundaryHeight)) return;
                _outerBoundaryHeight = value;
                OnPropertyChanged();
            }
        }

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

            if (Radius <= 0 || ToolDiameter <= 0 || StepPercentOfTool <= 0)
            {
                RemoveOperationFromMain();
                return;
            }

            _operation.Direction = Direction;
            _operation.PocketStrategy = PocketStrategy;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.Radius = Radius;
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
            _operation.StepPercentOfTool = StepPercentOfTool;
            _operation.Decimals = Decimals;
            _operation.LineAngleDeg = LineAngleDeg;
            _operation.WallTaperAngleDeg = WallTaperAngleDeg;
            _operation.IsRoughingEnabled = IsRoughingEnabled;
            _operation.IsFinishingEnabled = IsFinishingEnabled;
            _operation.FinishAllowance = FinishAllowance;
            _operation.FinishingMode = FinishingMode;
            _operation.IsIslandMillingEnabled = IsIslandMillingEnabled;
            _operation.OuterBoundaryType = OuterBoundaryType;
            _operation.OuterBoundaryCenterX = OuterBoundaryCenterX;
            _operation.OuterBoundaryCenterY = OuterBoundaryCenterY;
            _operation.OuterBoundaryWidth = OuterBoundaryWidth;
            _operation.OuterBoundaryHeight = OuterBoundaryHeight;

            if (_operation.Metadata == null)
                _operation.Metadata = new System.Collections.Generic.Dictionary<string, object>();

            _operation.Metadata["Direction"] = Direction;
            _operation.Metadata["PocketStrategy"] = PocketStrategy;
            _operation.Metadata["CenterX"] = CenterX;
            _operation.Metadata["CenterY"] = CenterY;
            _operation.Metadata["Radius"] = Radius;
            _operation.Metadata["TotalDepth"] = TotalDepth;
            _operation.Metadata["StepDepth"] = StepDepth;
            _operation.Metadata["ToolDiameter"] = ToolDiameter;
            _operation.Metadata["ContourHeight"] = ContourHeight;
            _operation.Metadata["FeedXYRapid"] = FeedXYRapid;
            _operation.Metadata["FeedXYWork"] = FeedXYWork;
            _operation.Metadata["FeedZRapid"] = FeedZRapid;
            _operation.Metadata["FeedZWork"] = FeedZWork;
            _operation.Metadata["SafeZHeight"] = SafeZHeight;
            _operation.Metadata["RetractHeight"] = RetractHeight;
            _operation.Metadata["StepPercentOfTool"] = StepPercentOfTool;
            _operation.Metadata["Decimals"] = Decimals;
            _operation.Metadata["LineAngleDeg"] = LineAngleDeg;
            _operation.Metadata["WallTaperAngleDeg"] = WallTaperAngleDeg;
            _operation.Metadata["IsRoughingEnabled"] = IsRoughingEnabled;
            _operation.Metadata["IsFinishingEnabled"] = IsFinishingEnabled;
            _operation.Metadata["FinishAllowance"] = FinishAllowance;
            _operation.Metadata["FinishingMode"] = FinishingMode;
            _operation.Metadata["IsIslandMillingEnabled"] = IsIslandMillingEnabled;
            _operation.Metadata["OuterBoundaryType"] = OuterBoundaryType;
            _operation.Metadata["OuterBoundaryCenterX"] = OuterBoundaryCenterX;
            _operation.Metadata["OuterBoundaryCenterY"] = OuterBoundaryCenterY;
            _operation.Metadata["OuterBoundaryWidth"] = OuterBoundaryWidth;
            _operation.Metadata["OuterBoundaryHeight"] = OuterBoundaryHeight;

            PocketOperationsViewModel?.MainViewModel?.NotifyOperationsChanged();
        }

        private void RemoveOperationFromMain()
        {
            if (PocketOperationsViewModel != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    PocketOperationsViewModel.RemoveOperation(_operation);
                }
                else
                {
                    dispatcher.Invoke(() => PocketOperationsViewModel.RemoveOperation(_operation));
                }
            }
        }
    }
}


