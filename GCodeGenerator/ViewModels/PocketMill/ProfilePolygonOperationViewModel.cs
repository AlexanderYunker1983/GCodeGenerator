using System;
using System.Windows.Threading;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfilePolygonOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfilePolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            DisplayName = _localizationManager?.GetString("ProfilePolygonName") ?? "";
        }

        public ProfileMillingOperationsViewModel ProfileMillingOperationsViewModel { get; set; }

        private ProfilePolygonOperation _operation;

        public ProfilePolygonOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                // Initialize from metadata if available, otherwise use defaults
                if (_operation.Metadata != null && _operation.Metadata.ContainsKey("ToolPathMode"))
                {
                    ToolPathMode = (ToolPathMode)_operation.Metadata["ToolPathMode"];
                    Direction = (MillingDirection)_operation.Metadata["Direction"];
                    CenterX = Convert.ToDouble(_operation.Metadata["CenterX"]);
                    CenterY = Convert.ToDouble(_operation.Metadata["CenterY"]);
                    NumberOfSides = Convert.ToInt32(_operation.Metadata["NumberOfSides"]);
                    Radius = Convert.ToDouble(_operation.Metadata["Radius"]);
                    RotationAngle = Convert.ToDouble(_operation.Metadata["RotationAngle"]);
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
                    EntryMode = (EntryMode)_operation.Metadata["EntryMode"];
                    EntryAngle = Convert.ToDouble(_operation.Metadata["EntryAngle"]);
                    SafeDistanceBetweenPasses = Convert.ToDouble(_operation.Metadata["SafeDistanceBetweenPasses"]);
                }
                else
                {
                    // Use operation properties directly
                    ToolPathMode = _operation.ToolPathMode;
                    Direction = _operation.Direction;
                    CenterX = _operation.CenterX;
                    CenterY = _operation.CenterY;
                    NumberOfSides = _operation.NumberOfSides;
                    Radius = _operation.Radius;
                    RotationAngle = _operation.RotationAngle;
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

        private int _numberOfSides = 6;
        public int NumberOfSides
        {
            get => _numberOfSides;
            set
            {
                if (value == _numberOfSides) return;
                _numberOfSides = value < 3 ? 3 : value;
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

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

            // Remove operation if no valid parameters
            if (NumberOfSides < 3 || Radius <= 0 || ToolDiameter <= 0)
            {
                RemoveOperationFromMain();
                return;
            }

            // Save to operation
            _operation.ToolPathMode = ToolPathMode;
            _operation.Direction = Direction;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.NumberOfSides = NumberOfSides;
            _operation.Radius = Radius;
            _operation.RotationAngle = RotationAngle;
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

            // Save to metadata
            if (_operation.Metadata == null)
                _operation.Metadata = new System.Collections.Generic.Dictionary<string, object>();
            
            _operation.Metadata["ToolPathMode"] = ToolPathMode;
            _operation.Metadata["Direction"] = Direction;
            _operation.Metadata["CenterX"] = CenterX;
            _operation.Metadata["CenterY"] = CenterY;
            _operation.Metadata["NumberOfSides"] = NumberOfSides;
            _operation.Metadata["Radius"] = Radius;
            _operation.Metadata["RotationAngle"] = RotationAngle;
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
            _operation.Metadata["EntryMode"] = EntryMode;
            _operation.Metadata["EntryAngle"] = EntryAngle;
            _operation.Metadata["SafeDistanceBetweenPasses"] = SafeDistanceBetweenPasses;
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

