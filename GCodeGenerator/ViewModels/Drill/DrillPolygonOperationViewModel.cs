using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillPolygonOperationViewModel : CloseableViewModel, IHasDisplayName
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

                // Initialize from metadata if available, otherwise from holes
                if (_operation.Metadata != null && _operation.Metadata.ContainsKey("CenterX"))
                {
                    CenterX = Convert.ToDouble(_operation.Metadata["CenterX"]);
                    CenterY = Convert.ToDouble(_operation.Metadata["CenterY"]);
                    Z = Convert.ToDouble(_operation.Metadata["Z"]);
                    Radius = Convert.ToDouble(_operation.Metadata["Radius"]);
                    NumberOfSides = Convert.ToInt32(_operation.Metadata["NumberOfSides"]);
                    HolesPerSide = Convert.ToInt32(_operation.Metadata["HolesPerSide"]);
                    RotationAngle = Convert.ToDouble(_operation.Metadata["RotationAngle"]);
                    TotalDepth = Convert.ToDouble(_operation.Metadata["TotalDepth"]);
                    StepDepth = Convert.ToDouble(_operation.Metadata["StepDepth"]);
                    FeedZRapid = Convert.ToDouble(_operation.Metadata["FeedZRapid"]);
                    FeedZWork = Convert.ToDouble(_operation.Metadata["FeedZWork"]);
                    RetractHeight = Convert.ToDouble(_operation.Metadata["RetractHeight"]);
                }
                else if (_operation.Holes.Any())
                {
                    var first = _operation.Holes.First();
                    CenterX = first.X;
                    CenterY = first.Y;
                    Z = first.Z;
                    TotalDepth = first.TotalDepth;
                    StepDepth = first.StepDepth;
                    FeedZRapid = first.FeedZRapid;
                    FeedZWork = first.FeedZWork;
                    RetractHeight = first.RetractHeight;
                    // Default values for missing parameters
                    Radius = 10;
                    NumberOfSides = 6;
                    HolesPerSide = 2;
                    RotationAngle = 0;
                }
                else
                {
                    CenterX = 0;
                    CenterY = 0;
                    Z = 0;
                    Radius = 10;
                    NumberOfSides = 6;
                    HolesPerSide = 2;
                    RotationAngle = 0;
                    TotalDepth = 2;
                    StepDepth = 1;
                    FeedZRapid = 500;
                    FeedZWork = 200;
                    RetractHeight = 0.3;
                }

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

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
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

            // Save operation-specific parameters to metadata.
            if (_operation.Metadata == null)
                _operation.Metadata = new System.Collections.Generic.Dictionary<string, object>();
            
            _operation.Metadata["CenterX"] = CenterX;
            _operation.Metadata["CenterY"] = CenterY;
            _operation.Metadata["Z"] = Z;
            _operation.Metadata["Radius"] = Radius;
            _operation.Metadata["NumberOfSides"] = NumberOfSides;
            _operation.Metadata["HolesPerSide"] = HolesPerSide;
            _operation.Metadata["RotationAngle"] = RotationAngle;
            _operation.Metadata["TotalDepth"] = TotalDepth;
            _operation.Metadata["StepDepth"] = StepDepth;
            _operation.Metadata["FeedZRapid"] = FeedZRapid;
            _operation.Metadata["FeedZWork"] = FeedZWork;
            _operation.Metadata["RetractHeight"] = RetractHeight;

            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }

        private void RemoveOperationFromMain()
        {
            if (MainViewModel != null)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
                if (dispatcher.CheckAccess())
                {
                    MainViewModel.RemoveOperation(_operation);
                }
                else
                {
                    dispatcher.Invoke(() => MainViewModel.RemoveOperation(_operation));
                }
            }
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

