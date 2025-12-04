using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class DrillPackageOperationViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;

        public DrillPackageOperationViewModel(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            var title = _localizationManager?.GetString("AddDrillPackage");
            DisplayName = string.IsNullOrEmpty(title) ? "Сверление под стандартный корпус" : title;

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
                    RotationAngle = Convert.ToDouble(_operation.Metadata["RotationAngle"]);
                    TotalDepth = Convert.ToDouble(_operation.Metadata["TotalDepth"]);
                    StepDepth = Convert.ToDouble(_operation.Metadata["StepDepth"]);
                    FeedZRapid = Convert.ToDouble(_operation.Metadata["FeedZRapid"]);
                    FeedZWork = Convert.ToDouble(_operation.Metadata["FeedZWork"]);
                    RetractHeight = Convert.ToDouble(_operation.Metadata["RetractHeight"]);
                    
                    // Restore package selection
                    if (_operation.Metadata.ContainsKey("PackageName"))
                    {
                        var packageName = _operation.Metadata["PackageName"] as string;
                        if (!string.IsNullOrEmpty(packageName))
                        {
                            SelectedPackage = Packages.FirstOrDefault(p => p.Name == packageName);
                        }
                    }
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
                    RotationAngle = 0;
                }
                else
                {
                    CenterX = 0;
                    CenterY = 0;
                    Z = 0;
                    RotationAngle = 0;
                    TotalDepth = 2;
                    StepDepth = 1;
                    FeedZRapid = 500;
                    FeedZWork = 200;
                    RetractHeight = 5;
                }

                FeedXYRapid = _operation.FeedXYRapid;
                FeedXYWork = _operation.FeedXYWork;
                SafeZBetweenHoles = _operation.SafeZBetweenHoles;
                Decimals = _operation.Decimals;

                RebuildHoles();
            }
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

        private double _safeZBetweenHoles = 5;
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

        private double _retractHeight = 5;
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

        protected override void OnClosed(IDataContext context)
        {
            base.OnClosed(context);
            if (_operation == null) return;

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
            _operation.Metadata["RotationAngle"] = RotationAngle;
            _operation.Metadata["TotalDepth"] = TotalDepth;
            _operation.Metadata["StepDepth"] = StepDepth;
            _operation.Metadata["FeedZRapid"] = FeedZRapid;
            _operation.Metadata["FeedZWork"] = FeedZWork;
            _operation.Metadata["RetractHeight"] = RetractHeight;
            if (SelectedPackage != null)
            {
                _operation.Metadata["PackageName"] = SelectedPackage.Name;
            }

            _operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                _operation.Holes.Add(hole);
        }
    }
}

