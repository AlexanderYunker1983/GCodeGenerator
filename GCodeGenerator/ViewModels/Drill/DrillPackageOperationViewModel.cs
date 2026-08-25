using System;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;

namespace GCodeGenerator.ViewModels.Drill
{
    public class DrillPackageOperationViewModel : OperationEditorViewModelBase<DrillPointsOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPackageOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("AddDrillPackage") ?? "AddDrillPackage";

            PreviewHoles = new ObservableCollection<DrillHole>();
            Packages = new ObservableCollection<PackageDefinition>();

            // Инициализация стандартных корпусов
            InitializePackages();

            // По умолчанию выбран DIP8
            SelectedPackage = Packages.FirstOrDefault(p => p.Name == "DIP8");
        }

        private void InitializePackages()
        {
            // DIP корпуса: шаг между выводами 2.54 мм, расстояние между рядами 7.62 мм (300 mil)
            Packages.Add(new PackageDefinition("DIP8", 4, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP14", 7, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP16", 8, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP18", 9, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP20", 10, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP24", 12, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP28", 14, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP32", 16, 2.54, 7.62));
            Packages.Add(new PackageDefinition("DIP40", 20, 2.54, 7.62));

            // TO-220: 3 вывода в один ряд, шаг 2.54 мм
            Packages.Add(new PackageDefinition("TO-220", 3, 2.54, 0));

            // TO-92: 3 вывода в один ряд, шаг 2.54 мм
            Packages.Add(new PackageDefinition("TO-92", 3, 2.54, 0));

            // SOIC корпуса: шаг 1.27 мм, расстояние между рядами 5.3 мм (для SOIC-8)
            Packages.Add(new PackageDefinition("SOIC-8", 4, 1.27, 5.3));
            Packages.Add(new PackageDefinition("SOIC-14", 7, 1.27, 5.3));
            Packages.Add(new PackageDefinition("SOIC-16", 8, 1.27, 5.3));
        }

        protected override void LoadFromOperation(DrillPointsOperation operation)
        {
            // Читаем типизированные свойства (пункт 3.3 плана): для новой
            // операции это дефолты модели, для загруженной — значения,
            // мигрированные из Metadata (пункт 3.2).
            CenterX = operation.CenterX;
            CenterY = operation.CenterY;
            Z = operation.Z;
            RotationAngle = operation.RotationAngle;
            TotalDepth = operation.TotalDepth;
            StepDepth = operation.StepDepth;
            FeedZRapid = operation.FeedZRapid;
            FeedZWork = operation.FeedZWork;
            RetractHeight = operation.RetractHeight;

            // Restore package selection; пустое имя — дефолт диалога (DIP8).
            if (!string.IsNullOrEmpty(operation.PackageName))
            {
                var package = Packages.FirstOrDefault(p => p.Name == operation.PackageName);
                if (package != null)
                    SelectedPackage = package;
            }

            FeedXYRapid = operation.FeedXYRapid;
            FeedXYWork = operation.FeedXYWork;
            SafeZBetweenHoles = operation.SafeZBetweenHoles;
            Decimals = operation.Decimals;

            RebuildHoles();
        }

        public ObservableCollection<DrillHole> PreviewHoles { get; }
        public ObservableCollection<PackageDefinition> Packages { get; }

        private PackageDefinition _selectedPackage;
        public PackageDefinition SelectedPackage
        {
            get => _selectedPackage;
            set
            {
                if (Equals(value, _selectedPackage)) return;
                _selectedPackage = value;
                OnPropertyChanged();
                RebuildHoles();
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

        private void RebuildHoles()
        {
            PreviewHoles.Clear();
            if (SelectedPackage == null) return;

            var angleRad = RotationAngle * Math.PI / 180.0;
            var cos = Math.Cos(angleRad);
            var sin = Math.Sin(angleRad);

            if (SelectedPackage.RowSpacing > 0)
            {
                // Двухрядный корпус (DIP, SOIC)
                var halfRowSpacing = SelectedPackage.RowSpacing / 2.0;
                var totalPinLength = (SelectedPackage.PinsPerRow - 1) * SelectedPackage.PinPitch;
                var halfPinLength = totalPinLength / 2.0;

                // Левый ряд (от вывода 1 до N/2)
                for (int i = 0; i < SelectedPackage.PinsPerRow; i++)
                {
                    var localX = -halfRowSpacing;
                    var localY = -halfPinLength + i * SelectedPackage.PinPitch;

                    var x = CenterX + localX * cos - localY * sin;
                    var y = CenterY + localX * sin + localY * cos;

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

                // Правый ряд (от вывода N/2+1 до N)
                for (int i = 0; i < SelectedPackage.PinsPerRow; i++)
                {
                    var localX = halfRowSpacing;
                    var localY = halfPinLength - i * SelectedPackage.PinPitch;

                    var x = CenterX + localX * cos - localY * sin;
                    var y = CenterY + localX * sin + localY * cos;

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
            else
            {
                // Однорядный корпус (TO-220, TO-92)
                var totalPinLength = (SelectedPackage.PinsPerRow - 1) * SelectedPackage.PinPitch;
                var halfPinLength = totalPinLength / 2.0;

                for (int i = 0; i < SelectedPackage.PinsPerRow; i++)
                {
                    var localX = 0.0;
                    var localY = -halfPinLength + i * SelectedPackage.PinPitch;

                    var x = CenterX + localX * cos - localY * sin;
                    var y = CenterY + localX * sin + localY * cos;

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

        protected override void ApplyToOperation()
        {
            Operation.FeedXYRapid = FeedXYRapid;
            Operation.FeedXYWork = FeedXYWork;
            Operation.SafeZBetweenHoles = SafeZBetweenHoles;
            Operation.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            Operation.DrillMode = DrillMode.Package;
            Operation.CenterX = CenterX;
            Operation.CenterY = CenterY;
            Operation.Z = Z;
            Operation.RotationAngle = RotationAngle;
            Operation.TotalDepth = TotalDepth;
            Operation.StepDepth = StepDepth;
            Operation.FeedZRapid = FeedZRapid;
            Operation.FeedZWork = FeedZWork;
            Operation.RetractHeight = RetractHeight;
            Operation.PackageName = SelectedPackage?.Name ?? string.Empty;

            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // корпус без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}

