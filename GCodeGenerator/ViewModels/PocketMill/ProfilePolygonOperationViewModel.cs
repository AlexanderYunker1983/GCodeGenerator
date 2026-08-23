using System;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.PocketMill
{
    public class ProfilePolygonOperationViewModel : OperationEditorViewModelBase<ProfilePolygonOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public ProfilePolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            DisplayName = _localizationManager?.GetString("ProfilePolygonName") ?? "";
        }

        protected override void LoadFromOperation(ProfilePolygonOperation operation)
        {
            // Читаем только типизированные свойства (пункт 3.5 плана):
            // легаси-Metadata мигрируется в свойства при загрузке (пункт 3.2).
            ToolPathMode = operation.ToolPathMode;
            Direction = operation.Direction;
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            NumberOfSides = operation.NumberOfSides;
            Radius = operation.Radius;
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
            EntryMode = operation.EntryMode;
            EntryAngle = operation.EntryAngle;
            SafeDistanceBetweenPasses = operation.SafeDistanceBetweenPasses;
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

        protected override void ApplyToOperation()
        {
            Operation.ToolPathMode = ToolPathMode;
            Operation.Direction = Direction;
            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.NumberOfSides = NumberOfSides;
            Operation.Radius = Radius;
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
            Operation.EntryMode = EntryMode;
            Operation.EntryAngle = EntryAngle;
            Operation.SafeDistanceBetweenPasses = SafeDistanceBetweenPasses;
            Operation.Decimals = Decimals;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => NumberOfSides >= 3 && Radius > 0 && ToolDiameter > 0;
    }
}

