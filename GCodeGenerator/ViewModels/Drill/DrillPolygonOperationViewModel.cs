using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillPolygonOperationViewModel : CloseableViewModel, IHasDisplayName, IDrillDialogViewModel
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillPolygon");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление по правильному многоугольнику" : title;

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
                Radius = _operation.Radius;
                NumberOfSides = _operation.NumberOfSides;
                HolesPerSide = _operation.HolesPerSide;
                RotationAngle = _operation.RotationAngle;
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

        private double _radius;
        public double Radius
        {
            get => _radius;
            set
            {
                if (value.Equals(_radius)) return;
                _radius = value;
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _numberOfSides = 6;
        public int NumberOfSides
        {
            get => _numberOfSides;
            set
            {
                if (value == _numberOfSides) return;
                _numberOfSides = Math.Max(3, value);
                OnPropertyChanged();
                RebuildHoles();
            }
        }

        private int _holesPerSide = 2;
        public int HolesPerSide
        {
            get => _holesPerSide;
            set
            {
                if (value == _holesPerSide) return;
                _holesPerSide = Math.Max(1, value);
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
            _operation.DrillMode = DrillMode.Polygon;
            _operation.CenterX = CenterX;
            _operation.CenterY = CenterY;
            _operation.Z = Z;
            _operation.Radius = Radius;
            _operation.NumberOfSides = NumberOfSides;
            _operation.HolesPerSide = HolesPerSide;
            _operation.RotationAngle = RotationAngle;
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
            if (NumberOfSides < 3 || Radius == 0 || HolesPerSide < 1)
                return;

            var rotationRad = RotationAngle * Math.PI / 180.0;
            var angleStep = 2 * Math.PI / NumberOfSides;
            
            // Calculate vertices of the polygon
            var vertices = new System.Collections.Generic.List<(double x, double y)>();
            for (int i = 0; i < NumberOfSides; i++)
            {
                var angle = i * angleStep + rotationRad;
                var x = CenterX + Radius * Math.Cos(angle);
                var y = CenterY + Radius * Math.Sin(angle);
                vertices.Add((x, y));
            }

            // Distribute holes evenly along each side
            for (int side = 0; side < NumberOfSides; side++)
            {
                var startVertex = vertices[side];
                var endVertex = vertices[(side + 1) % NumberOfSides];
                
                // Calculate step along the side
                var dx = endVertex.x - startVertex.x;
                var dy = endVertex.y - startVertex.y;
                
                // First hole is exactly at the polygon vertex (startVertex).
                // Remaining holes are distributed evenly along the side, excluding the end vertex
                // to avoid duplicates when moving to the next side.
                var stepX = HolesPerSide > 1 ? dx / HolesPerSide : 0;
                var stepY = HolesPerSide > 1 ? dy / HolesPerSide : 0;
                
                for (int holeIndex = 0; holeIndex < HolesPerSide; holeIndex++)
                {
                    var x = startVertex.x + stepX * holeIndex;
                    var y = startVertex.y + stepY * holeIndex;

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
}

