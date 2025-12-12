using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.ViewModels;
using YLocalization;

namespace GCodeGenerator.ViewModels
{
    public class SettingsViewModel : CloseableViewModel, IHasDisplayName
    {
        private readonly GCodeSettings _settings;

        public SettingsViewModel()
            : this(null)
        {
        }

        public SettingsViewModel(ILocalizationManager localizationManager)
        {
            _settings = GCodeSettingsStore.Current;

            var title = localizationManager?.GetString("GCodeSettingsTitle");
            DisplayName = string.IsNullOrEmpty(title) ? "Настройки G-кода" : title;

            // Initialize from shared settings
            UseLineNumbers = _settings.UseLineNumbers;
            LineNumberStart = _settings.LineNumberStart;
            LineNumberStep = _settings.LineNumberStep;
            UseComments = _settings.UseComments;
            AllowArcs = _settings.AllowArcs;
            UsePaddedGCodes = _settings.UsePaddedGCodes;
            UseDarkTheme = _settings.UseDarkTheme;
            SpindleControlEnabled = _settings.SpindleControlEnabled;
            SpindleSpeedEnabled = _settings.SpindleSpeedEnabled;
            SpindleSpeedRpm = _settings.SpindleSpeedRpm;
            SpindleStartEnabled = _settings.SpindleStartEnabled;
            SpindleStartCommand = _settings.SpindleStartCommand;
            SpindleStopEnabled = _settings.SpindleStopEnabled;
            SpindleDelayEnabled = _settings.SpindleDelayEnabled;
            SpindleDelaySeconds = _settings.SpindleDelaySeconds;
            CoolantControlEnabled = _settings.CoolantControlEnabled;
            CoolantStartEnabled = _settings.CoolantStartEnabled;
            CoolantStopEnabled = _settings.CoolantStopEnabled;
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
                ThemeHelper.ApplyTheme(value);
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

        protected override void OnClosed(MugenMvvmToolkit.Interfaces.Models.IDataContext context)
        {
            base.OnClosed(context);

            // Apply changes back to shared settings when window is closed
            _settings.UseLineNumbers = UseLineNumbers;
            _settings.LineNumberStart = LineNumberStart;
            _settings.LineNumberStep = LineNumberStep;
            _settings.UseComments = UseComments;
            _settings.AllowArcs = AllowArcs;
            _settings.UsePaddedGCodes = UsePaddedGCodes;
            _settings.UseDarkTheme = UseDarkTheme;
            _settings.SpindleControlEnabled = SpindleControlEnabled;
            _settings.SpindleSpeedEnabled = SpindleSpeedEnabled;
            _settings.SpindleSpeedRpm = SpindleSpeedRpm;
            _settings.SpindleStartEnabled = SpindleStartEnabled;
            _settings.SpindleStartCommand = SpindleStartCommand;
            _settings.SpindleStopEnabled = SpindleStopEnabled;
            _settings.SpindleDelayEnabled = SpindleDelayEnabled;
            _settings.SpindleDelaySeconds = SpindleDelaySeconds;
            _settings.CoolantControlEnabled = CoolantControlEnabled;
            _settings.CoolantStartEnabled = CoolantStartEnabled;
            _settings.CoolantStopEnabled = CoolantStopEnabled;
            GCodeSettingsStore.Save();
        }
    }
}


