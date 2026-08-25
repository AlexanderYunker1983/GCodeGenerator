using GCodeGenerator.Models;
using GCodeGenerator.Localization;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    public class SettingsViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly GCodeSettings _settings;
        private readonly ISettingsStore _settingsStore;
        private readonly IThemeService _themeService;

        public SettingsViewModel()
            : this(null, null, null)
        {
        }

        public SettingsViewModel(ILocalizationManager localizationManager, ISettingsStore settingsStore, IThemeService themeService)
        {
            // Настройки и тема поступают через IoC. Безаргументный конструктор —
            // для XAML-дизайнера: фолбэк на настройки по умолчанию.
            _settings = settingsStore?.Current ?? new GCodeSettings();
            _settingsStore = settingsStore;
            _themeService = themeService;

            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("GCodeSettingsTitle") ?? "GCodeSettingsTitle";

            // Initialize from shared settings
            // Пункт 8.1: тематические группы настроек.
            var format = _settings.Format;
            var ui = _settings.Ui;
            var spindle = _settings.Spindle;
            var coolant = _settings.Coolant;
            var workCoordinate = _settings.WorkCoordinate;
            UseLineNumbers = format.UseLineNumbers;
            LineNumberStart = format.LineNumberStart;
            LineNumberStep = format.LineNumberStep;
            UseComments = format.UseComments;
            AllowArcs = format.AllowArcs;
            UsePaddedGCodes = format.UsePaddedGCodes;
            UseDarkTheme = ui.UseDarkTheme;
            SpindleControlEnabled = spindle.SpindleControlEnabled;
            SpindleSpeedEnabled = spindle.SpindleSpeedEnabled;
            SpindleSpeedRpm = spindle.SpindleSpeedRpm;
            SpindleStartEnabled = spindle.SpindleStartEnabled;
            SpindleStartCommand = spindle.SpindleStartCommand;
            SpindleStopEnabled = spindle.SpindleStopEnabled;
            SpindleDelayEnabled = spindle.SpindleDelayEnabled;
            SpindleDelaySeconds = spindle.SpindleDelaySeconds;
            CoolantControlEnabled = coolant.CoolantControlEnabled;
            CoolantStartEnabled = coolant.CoolantStartEnabled;
            CoolantStopEnabled = coolant.CoolantStopEnabled;
            AddStartPosition = workCoordinate.AddStartPosition;
            StartX = workCoordinate.StartX;
            StartY = workCoordinate.StartY;
            StartZ = workCoordinate.StartZ;
            AddEndPosition = workCoordinate.AddEndPosition;
            EndX = workCoordinate.EndX;
            EndY = workCoordinate.EndY;
            EndZ = workCoordinate.EndZ;
            SetWorkCoordinateSystem = workCoordinate.SetWorkCoordinateSystem;
            WorkCoordinateSystem = workCoordinate.WorkCoordinateSystem ?? "G54";
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

        private bool _useLineNumbers;

        public bool UseLineNumbers
        {
            get => _useLineNumbers;
            set
            {
                if (value == _useLineNumbers) return;
                _useLineNumbers = value;
                OnPropertyChanged();
            }
        }

        private int _lineNumberStart;

        public int LineNumberStart
        {
            get => _lineNumberStart;
            set
            {
                if (value == _lineNumberStart) return;
                _lineNumberStart = value;
                OnPropertyChanged();
            }
        }

        private int _lineNumberStep;

        public int LineNumberStep
        {
            get => _lineNumberStep;
            set
            {
                if (value == _lineNumberStep) return;
                _lineNumberStep = value;
                OnPropertyChanged();
            }
        }

        private bool _useComments;

        public bool UseComments
        {
            get => _useComments;
            set
            {
                if (value == _useComments) return;
                _useComments = value;
                OnPropertyChanged();
            }
        }

        private bool _allowArcs;

        public bool AllowArcs
        {
            get => _allowArcs;
            set
            {
                if (value == _allowArcs) return;
                _allowArcs = value;
                OnPropertyChanged();
            }
        }

        private bool _usePaddedGCodes;

        public bool UsePaddedGCodes
        {
            get => _usePaddedGCodes;
            set
            {
                if (value == _usePaddedGCodes) return;
                _usePaddedGCodes = value;
                OnPropertyChanged();
            }
        }

        private bool _useDarkTheme;

        public bool UseDarkTheme
        {
            get => _useDarkTheme;
            set
            {
                if (value == _useDarkTheme) return;
                _useDarkTheme = value;
                OnPropertyChanged();
                _themeService?.ApplyTheme(value);
            }
        }

        private bool _spindleControlEnabled;
        public bool SpindleControlEnabled
        {
            get => _spindleControlEnabled;
            set
            {
                if (value == _spindleControlEnabled) return;
                _spindleControlEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _spindleSpeedEnabled;
        public bool SpindleSpeedEnabled
        {
            get => _spindleSpeedEnabled;
            set
            {
                if (value == _spindleSpeedEnabled) return;
                _spindleSpeedEnabled = value;
                OnPropertyChanged();
            }
        }

        private int _spindleSpeedRpm;
        public int SpindleSpeedRpm
        {
            get => _spindleSpeedRpm;
            set
            {
                if (value == _spindleSpeedRpm) return;
                _spindleSpeedRpm = value;
                OnPropertyChanged();
            }
        }

        private bool _spindleStartEnabled;
        public bool SpindleStartEnabled
        {
            get => _spindleStartEnabled;
            set
            {
                if (value == _spindleStartEnabled) return;
                _spindleStartEnabled = value;
                OnPropertyChanged();
            }
        }

        private string _spindleStartCommand;
        public string SpindleStartCommand
        {
            get => _spindleStartCommand;
            set
            {
                if (value == _spindleStartCommand) return;
                _spindleStartCommand = value;
                OnPropertyChanged();
            }
        }

        private bool _spindleStopEnabled;
        public bool SpindleStopEnabled
        {
            get => _spindleStopEnabled;
            set
            {
                if (value == _spindleStopEnabled) return;
                _spindleStopEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _spindleDelayEnabled;
        public bool SpindleDelayEnabled
        {
            get => _spindleDelayEnabled;
            set
            {
                if (value == _spindleDelayEnabled) return;
                _spindleDelayEnabled = value;
                OnPropertyChanged();
            }
        }

        private double _spindleDelaySeconds;
        public double SpindleDelaySeconds
        {
            get => _spindleDelaySeconds;
            set
            {
                if (value.Equals(_spindleDelaySeconds)) return;
                _spindleDelaySeconds = value;
                OnPropertyChanged();
            }
        }

        private bool _coolantControlEnabled;
        public bool CoolantControlEnabled
        {
            get => _coolantControlEnabled;
            set
            {
                if (value == _coolantControlEnabled) return;
                _coolantControlEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _coolantStartEnabled;
        public bool CoolantStartEnabled
        {
            get => _coolantStartEnabled;
            set
            {
                if (value == _coolantStartEnabled) return;
                _coolantStartEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _coolantStopEnabled;
        public bool CoolantStopEnabled
        {
            get => _coolantStopEnabled;
            set
            {
                if (value == _coolantStopEnabled) return;
                _coolantStopEnabled = value;
                OnPropertyChanged();
            }
        }

        private bool _addStartPosition;
        public bool AddStartPosition
        {
            get => _addStartPosition;
            set
            {
                if (value == _addStartPosition) return;
                _addStartPosition = value;
                OnPropertyChanged();
            }
        }

        private double _startX;
        public double StartX
        {
            get => _startX;
            set
            {
                if (value.Equals(_startX)) return;
                _startX = value;
                OnPropertyChanged();
            }
        }

        private double _startY;
        public double StartY
        {
            get => _startY;
            set
            {
                if (value.Equals(_startY)) return;
                _startY = value;
                OnPropertyChanged();
            }
        }

        private double _startZ;
        public double StartZ
        {
            get => _startZ;
            set
            {
                if (value.Equals(_startZ)) return;
                _startZ = value;
                OnPropertyChanged();
            }
        }

        private bool _addEndPosition;
        public bool AddEndPosition
        {
            get => _addEndPosition;
            set
            {
                if (value == _addEndPosition) return;
                _addEndPosition = value;
                OnPropertyChanged();
            }
        }

        private double _endX;
        public double EndX
        {
            get => _endX;
            set
            {
                if (value.Equals(_endX)) return;
                _endX = value;
                OnPropertyChanged();
            }
        }

        private double _endY;
        public double EndY
        {
            get => _endY;
            set
            {
                if (value.Equals(_endY)) return;
                _endY = value;
                OnPropertyChanged();
            }
        }

        private double _endZ;
        public double EndZ
        {
            get => _endZ;
            set
            {
                if (value.Equals(_endZ)) return;
                _endZ = value;
                OnPropertyChanged();
            }
        }

        public override void OnClosed()
        {
            base.OnClosed();

            // Apply changes back to shared settings when window is closed
            // Пункт 8.1: тематические группы настроек.
            var format = _settings.Format;
            var ui = _settings.Ui;
            var spindle = _settings.Spindle;
            var coolant = _settings.Coolant;
            var workCoordinate = _settings.WorkCoordinate;
            format.UseLineNumbers = UseLineNumbers;
            format.LineNumberStart = LineNumberStart;
            format.LineNumberStep = LineNumberStep;
            format.UseComments = UseComments;
            format.AllowArcs = AllowArcs;
            format.UsePaddedGCodes = UsePaddedGCodes;
            ui.UseDarkTheme = UseDarkTheme;
            spindle.SpindleControlEnabled = SpindleControlEnabled;
            spindle.SpindleSpeedEnabled = SpindleSpeedEnabled;
            spindle.SpindleSpeedRpm = SpindleSpeedRpm;
            spindle.SpindleStartEnabled = SpindleStartEnabled;
            spindle.SpindleStartCommand = SpindleStartCommand;
            spindle.SpindleStopEnabled = SpindleStopEnabled;
            spindle.SpindleDelayEnabled = SpindleDelayEnabled;
            spindle.SpindleDelaySeconds = SpindleDelaySeconds;
            coolant.CoolantControlEnabled = CoolantControlEnabled;
            coolant.CoolantStartEnabled = CoolantStartEnabled;
            coolant.CoolantStopEnabled = CoolantStopEnabled;
            workCoordinate.AddStartPosition = AddStartPosition;
            workCoordinate.StartX = StartX;
            workCoordinate.StartY = StartY;
            workCoordinate.StartZ = StartZ;
            workCoordinate.AddEndPosition = AddEndPosition;
            workCoordinate.EndX = EndX;
            workCoordinate.EndY = EndY;
            workCoordinate.EndZ = EndZ;
            workCoordinate.SetWorkCoordinateSystem = SetWorkCoordinateSystem;
            workCoordinate.WorkCoordinateSystem = WorkCoordinateSystem ?? "G54";
            _settingsStore?.Save();
        }

        private bool _setWorkCoordinateSystem;
        public bool SetWorkCoordinateSystem
        {
            get => _setWorkCoordinateSystem;
            set
            {
                if (value == _setWorkCoordinateSystem) return;
                _setWorkCoordinateSystem = value;
                OnPropertyChanged();
            }
        }

        private string _workCoordinateSystem;
        public string WorkCoordinateSystem
        {
            get => _workCoordinateSystem;
            set
            {
                if (value == _workCoordinateSystem) return;
                _workCoordinateSystem = value;
                OnPropertyChanged();
            }
        }
    }
}


