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
        }
    }
}


