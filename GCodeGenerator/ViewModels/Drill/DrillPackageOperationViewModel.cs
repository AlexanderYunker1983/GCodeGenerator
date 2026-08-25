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

            // Перечень корпусов принадлежит ядру: по имени корпуса, сохранённому
            // в проекте, отверстия должны пересчитываться и без открытого диалога.
            Packages = new ObservableCollection<PackageDefinition>(PackageCatalog.All);
            SelectedPackage = PackageCatalog.FindOrDefault(PackageCatalog.DefaultPackageName);
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

        /// <summary>
        /// Пересчитывает отверстия шаблона для предпросмотра. Расчёт выполняет
        /// ядро (<see cref="DrillPatternBuilder"/>), поэтому диалог и сохранённая
        /// операция описывают одни и те же отверстия.
        /// </summary>
        private void RebuildHoles()
        {
            PreviewHoles.Clear();

            var pattern = new DrillPointsOperation();
            ApplyPatternParameters(pattern);
            foreach (var hole in DrillPatternBuilder.Build(pattern))
                PreviewHoles.Add(hole);
        }

        protected override void ApplyToOperation()
        {
            ApplyPatternParameters(Operation);

            Operation.Holes.Clear();
            foreach (var hole in PreviewHoles)
                Operation.Holes.Add(hole);
        }

        /// <summary>
        /// Переносит параметры шаблона из диалога в операцию. Используется и при
        /// сохранении, и при предварительном расчёте отверстий, поэтому диалог
        /// и файл проекта описывают шаблон одинаково.
        /// </summary>
        private void ApplyPatternParameters(DrillPointsOperation target)
        {
            target.FeedXYRapid = FeedXYRapid;
            target.FeedXYWork = FeedXYWork;
            target.SafeZBetweenHoles = SafeZBetweenHoles;
            target.Decimals = Decimals;

            // Save operation-specific parameters to typed properties (пункт 3.3).
            target.DrillMode = DrillMode.Package;
            target.CenterX = CenterX;
            target.CenterY = CenterY;
            target.Z = Z;
            target.RotationAngle = RotationAngle;
            target.TotalDepth = TotalDepth;
            target.StepDepth = StepDepth;
            target.FeedZRapid = FeedZRapid;
            target.FeedZWork = FeedZWork;
            target.RetractHeight = RetractHeight;
            target.PackageName = SelectedPackage?.Name ?? string.Empty;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3):
        // корпус без отверстий не имеет смысла.
        protected override bool IsValid() => PreviewHoles.Count > 0;
    }
}

