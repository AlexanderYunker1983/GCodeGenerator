using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels.Pocket
{
    public class PocketDxfOperationViewModel : OperationEditorViewModelBase<PocketDxfOperation>, IHasDisplayName
    {
        private readonly ILocalizationManager _localizationManager;
        private readonly IDialogService _dialogService;
        private readonly IDxfImportService _dxfImportService;
        private readonly IAppLogger _logger;

        public PocketDxfOperationViewModel(
            ILocalizationManager localizationManager,
            IDialogService dialogService,
            IDxfImportService dxfImportService,
            IAppLogger logger = null)
        {
            _localizationManager = localizationManager;
            _dialogService = dialogService;
            _dxfImportService = dxfImportService ?? throw new ArgumentNullException(nameof(dxfImportService));
            _logger = logger ?? NullAppLogger.Instance;
            // Пункт 8.4 плана: импорт DXF — async: парсинг файла выполняется в пуле (Task.Run), UI-поток не блокируется даже на больших файлах.
            ImportDxfCommand = new AsyncRelayCommand(ImportDxfFileAsync);
            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = _localizationManager?.GetString("PocketDxfName") ?? "PocketDxfName";

            // Пункт 7.3: операция по умолчанию для автономного создания
            // (в потоках добавления/редактирования фабрику задаёт Operation).
            if (Operation == null)
                Operation = new PocketDxfOperation();
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

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (Equals(value, _filePath)) return;
                _filePath = value;
                OnPropertyChanged();
            }
        }

        private string _importInfo;
        public string ImportInfo
        {
            get => _importInfo;
            set
            {
                if (Equals(value, _importInfo)) return;
                _importInfo = value;
                OnPropertyChanged();
            }
        }

        public ICommand ImportDxfCommand { get; }

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
                // Ограничиваем угол диапазоном [0; 90)
                var v = Math.Max(0, Math.Min(89.999999, value));
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


        protected override void LoadFromOperation(PocketDxfOperation operation)
        {
            if (operation == null)
                return;

            FilePath = operation.DxfFilePath;

            if (operation.ClosedContours != null && operation.ClosedContours.Count > 0)
            {
                var contourCount = operation.ClosedContours.Count;
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "DxfImportContoursInfo";
                ImportInfo = string.Format(infoTemplate, contourCount);
            }
            else
            {
                ImportInfo = null;
            }

            Direction = operation.Direction;
            PocketStrategy = operation.PocketStrategy;
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
            StepPercentOfTool = operation.StepPercentOfTool;
            Decimals = operation.Decimals;
            LineAngleDeg = operation.LineAngleDeg;
            WallTaperAngleDeg = Math.Max(0, operation.WallTaperAngleDeg);
            IsRoughingEnabled = operation.IsRoughingEnabled;
            IsFinishingEnabled = operation.IsFinishingEnabled;
            FinishAllowance = operation.FinishAllowance;
            FinishingMode = operation.FinishingMode;
        }

        private async Task ImportDxfFileAsync()
        {
            var title = _localizationManager?.GetString("DxfImportDialogTitle") ?? "DxfImportDialogTitle";
            var fileName = _dialogService?.ShowOpenDialog(title, "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*", "dxf");
            if (fileName == null)
                return;

            try
            {
                var closedContours = await Task.Run(() => _dxfImportService.ReadPocketClosedContours(fileName));
                if (closedContours.Count == 0)
                {
                    _logger.Warning($"DXF import found no closed contours: {fileName}");
                    var msg = _localizationManager?.GetString("DxfImportNoClosedContours") ?? "DxfImportNoClosedContours";
                    _dialogService?.ShowInfo(msg, title);
                    return;
                }

                Operation.ClosedContours = closedContours;
                Operation.DxfFilePath = fileName;
                // Пункт 7.2 плана: импорт DXF перерисовывает 2D-превью
                // (ClosedContours — авто-свойство, без PropertyChanged).
                Operation.NotifyContentChanged();
                FilePath = fileName;
                var contourCount = closedContours.Count;
                var infoTemplate = _localizationManager?.GetString("DxfImportContoursInfo") ?? "DxfImportContoursInfo";
                ImportInfo = string.Format(infoTemplate, contourCount);
                _logger.Info($"DXF imported for pocket: {fileName} ({contourCount} closed contour(s))");
            }
            catch (Exception ex)
            {
                _logger.Error($"DXF import failed: {fileName}", ex);
                var msg = _localizationManager?.GetString("DxfImportFailed") ?? "DxfImportFailed";
                _dialogService?.ShowError($"{msg} {ex.Message}", title);
            }
        }

        protected override void ApplyToOperation()
        {
            Operation.Direction = Direction;
            Operation.PocketStrategy = PocketStrategy;
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
            Operation.StepPercentOfTool = StepPercentOfTool;
            Operation.Decimals = Decimals;
            Operation.LineAngleDeg = LineAngleDeg;
            Operation.WallTaperAngleDeg = WallTaperAngleDeg;
            Operation.IsRoughingEnabled = IsRoughingEnabled;
            Operation.IsFinishingEnabled = IsFinishingEnabled;
            Operation.FinishAllowance = FinishAllowance;
            Operation.FinishingMode = FinishingMode;
        }

        // Удаление операции при невалидных параметрах (legacy «remove if invalid», пункт 7.3).
        protected override bool IsValid() => ToolDiameter > 0 && StepPercentOfTool > 0
            && Operation.ClosedContours != null && Operation.ClosedContours.Count > 0;
    }
}
