using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillPolygonOperationViewModel : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName, IDrillDialogViewModel
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPolygonOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("AddDrillPolygon") ?? "AddDrillPolygon";

            PreviewHoles = new ObservableCollection<DrillHole>();
        }

        protected override void LoadFromOperation(DrillPointsOperation operation)
        {
            // Читаем типизированные свойства (пункт 3.3 плана): для новой
            // операции это дефолты модели, для загруженной — значения,
            // мигрированные из Metadata (пункт 3.2).
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            Radius = operation.Radius;
            NumberOfSides = operation.NumberOfSides;
            HolesPerSide = operation.HolesPerSide;
            RotationAngle = operation.RotationAngle;
            TotalDepth = operation.TotalDepth;
            StepDepth = operation.StepDepth;
            FeedZRapid = operation.FeedZRapid;
            FeedZWork = operation.FeedZWork;
            RetractHeight = operation.RetractHeight;

            FeedXYRapid = operation.FeedXYRapid;
            FeedXYWork = operation.FeedXYWork;
            SafeZBetweenHoles = operation.SafeZBetweenHoles;
            Decimals = operation.Decimals;

            RebuildHoles();
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

        protected override void ApplyToOperation()
        {
            Operation.FeedXYRapid = FeedXYRapid;
            Operation.FeedXYWork = FeedXYWork;
            Operation.SafeZBetweenHoles = SafeZBetweenHoles;
            Operation.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            Operation.DrillMode = DrillMode.Polygon;
            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.Z = Z;
            Operation.Radius = Radius;
            Operation.NumberOfSides = NumberOfSides;
            Operation.HolesPerSide = HolesPerSide;
            Operation.RotationAngle = RotationAngle;
            Operation.TotalDepth = TotalDepth;
            Operation.StepDepth = StepDepth;
            Operation.FeedZRapid = FeedZRapid;
            Operation.FeedZWork = FeedZWork;
            Operation.RetractHeight = RetractHeight;

            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // многоугольник без отверстий (NumberOfSides<3, Radius==0 или HolesPerSide<1) не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;

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

