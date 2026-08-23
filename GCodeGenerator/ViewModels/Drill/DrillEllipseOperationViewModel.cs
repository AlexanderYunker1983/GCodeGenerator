using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillEllipseOperationViewModel : CloseableViewModel, IHasDisplayName, IDrillDialogViewModel
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillEllipseOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillEllipse");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по эллипсу" : title;

            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        public DrillOperationsViewModel MainViewModel { get; set; }

        private DrillPointsOperation _operation;

        public DrillPointsOperation Operation
        {
            get => _operation;
            set
            {
                if (Equals(value, _operation)) return;
                _operation = value;
                if (_operation == null) return;

                // Читаем типизированные свойства (пункт 3.3 плана): для новой
                // операции это дефолты модели, для загруженной — значения,
                // мигрированные из Metadata (пункт 3.2).
                CenterX = _operation.CenterX;
                CenterY = _operation.CenterY;
                Z = _operation.Z;
                RadiusX = _operation.RadiusX;
                RadiusY = _operation.RadiusY;
                RotationAngle = _operation.RotationAngle;
                HoleCount = _operation.HoleCount;
                StartAngleDeg = _operation.StartAngleDeg;
                TotalDepth = _operation.TotalDepth;
                StepDepth = _operation.StepDepth;
                FeedZRapid = _operation.FeedZRapid;
                FeedZWork = _operation.FeedZWork;
                RetractHeight = _operation.RetractHeight;

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

        private double _centerX;
        public double CenterX
        {
            get => _centerX;
            set
            {
                if (value.Equals(_centerX)) return;
                _centerX = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _centerY;
        public double CenterY
        {
            get => _centerY;
            set
            {
                if (value.Equals(_centerY)) return;
                _centerY = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _z;
        public double Z
        {
            get => _z;
            set
            {
                if (value.Equals(_z)) return;
                _z = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _radiusX;
        public double RadiusX
        {
            get => _radiusX;
            set
            {
                if (value.Equals(_radiusX)) return;
                _radiusX = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _radiusY;
        public double RadiusY
        {
            get => _radiusY;
            set
            {
                if (value.Equals(_radiusY)) return;
                _radiusY = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private double _rotationAngle;
        public double RotationAngle
        {
            get => _rotationAngle;
            set
            {
                if (value.Equals(_rotationAngle)) return;
                _rotationAngle = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _holeCount = 2;
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

        private double _startAngleDeg;
        public double StartAngleDeg
        {
            get => _startAngleDeg;
            set
            {
                if (value.Equals(_startAngleDeg)) return;
                _startAngleDeg = value;
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

        private double _safeZBetweenHoles = 1;
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

        private double _retractHeight = 0.3;
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

        public override void OnClosed()
        {
            base.OnClosed();
            if (_operation == null) return;

            // Remove operation if no holes were created
            if (PreviewHoles.Count == 0)
            {
                RemoveOperationFromMain();
                return;
            }

            _operation.FeedXYRapid = FeedXYRapid;
            _operation.FeedXYWork = FeedXYWork;
            _operation.SafeZBetweenHoles = SafeZBetweenHoles;
            _operation.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            _operation.DrillMode = DrillMode.Ellipse;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.Z = Z;
            _operation.RadiusX = RadiusX;
            _operation.RadiusY = RadiusY;
            _operation.RotationAngle = RotationAngle;
            _operation.HoleCount = HoleCount;
            _operation.StartAngleDeg = StartAngleDeg;
            _operation.TotalDepth = TotalDepth;
            _operation.StepDepth = StepDepth;
            _operation.FeedZRapid = FeedZRapid;
            _operation.FeedZWork = FeedZWork;
            _operation.RetractHeight = RetractHeight;

            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }

        private void RemoveOperationFromMain()
        {
            // \u041f\u0443\u043d\u043a\u0442 7.1 \u043f\u043b\u0430\u043d\u0430: OnClosed \u0432\u044b\u0437\u044b\u0432\u0430\u0435\u0442\u0441\u044f IDialogService \u043d\u0430 UI-\u043f\u043e\u0442\u043e\u043a\u0435
            // \u043f\u043e\u0441\u043b\u0435 \u0437\u0430\u043a\u0440\u044b\u0442\u0438\u044f \u043c\u043e\u0434\u0430\u043b\u044c\u043d\u043e\u0433\u043e \u043e\u043a\u043d\u0430 (WpfDialogService.ShowDialog) \u2014
            // \u043f\u0435\u0440\u0435\u0445\u043e\u0434 \u0447\u0435\u0440\u0435\u0437 Dispatcher \u043d\u0435 \u043d\u0443\u0436\u0435\u043d, \u0432\u044b\u0437\u043e\u0432 \u043f\u0440\u044f\u043c\u043e\u0439.
            MainViewModel?.RemoveOperation(_operation);
        }

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (HoleCount < 2 || RadiusX == 0 || RadiusY == 0)
                return;

            var startRad = StartAngleDeg * Math.PI / 180.0;
            var stepRad = 2 * Math.PI / HoleCount;
            var rotationRad = RotationAngle * Math.PI / 180.0;
            var cosRot = Math.Cos(rotationRad);
            var sinRot = Math.Sin(rotationRad);

            for (int i = 0; i < HoleCount; i++)
            {
                var angle = startRad + stepRad * i;
                // Parametric equation of ellipse
                var xEllipse = RadiusX * Math.Cos(angle);
                var yEllipse = RadiusY * Math.Sin(angle);
                
                // Apply rotation
                var x = CenterX + xEllipse * cosRot - yEllipse * sinRot;
                var y = CenterY + xEllipse * sinRot + yEllipse * cosRot;

                var hole = new DrillHole
                {
                    X = x,
                    Y = y,
                    Z = Z,
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

