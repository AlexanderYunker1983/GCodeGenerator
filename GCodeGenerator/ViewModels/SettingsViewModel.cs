using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
using GCodeGenerator.Models;
using GCodeGenerator.Services;

namespace GCodeGenerator.ViewModels
{
    /// <summary>
    /// Диалог настроек генерации и интерфейса.
    ///
    /// Значения переносятся между настройками и окном по таблице
    /// <see cref="SettingsMapping"/> — той же, по которой они пишутся в
    /// постоянное хранилище. Раньше все 28 параметров перечислялись здесь
    /// дважды вручную, поэтому забытая строка означала настройку, которую
    /// окно показывает, но не сохраняет.
    ///
    /// Закрытие окна больше не сохраняет: сохраняет OK, а отмена и крестик
    /// оставляют настройки нетронутыми — как в диалогах операций.
    /// </summary>
    public partial class SettingsViewModel : CloseableViewModel, IHasDisplayName
    {
        /// <summary>Система координат по умолчанию: пустое значение равнозначно G54.</summary>
        private const string DefaultWorkCoordinateSystem = "G54";

        private readonly GCodeSettings _settings;
        private readonly ISettingsStore _settingsStore;
        private readonly IThemeService _themeService;

        /// <summary>Тема на момент открытия окна — к ней возвращает отмена.</summary>
        private readonly bool _initialDarkTheme;

        private bool _isAccepted;

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

            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(RequestClose);

            LoadFromSettings(_settings);
            _initialDarkTheme = UseDarkTheme;
        }

        [ObservableProperty]
        private string _displayName;

        [ObservableProperty]
        private bool _useLineNumbers;

        [ObservableProperty]
        private int _lineNumberStart;

        [ObservableProperty]
        private int _lineNumberStep;

        [ObservableProperty]
        private bool _useComments;

        [ObservableProperty]
        private bool _allowArcs;

        [ObservableProperty]
        private bool _usePaddedGCodes;

        [ObservableProperty]
        private bool _useDarkTheme;

        [ObservableProperty]
        private bool _spindleControlEnabled;

        [ObservableProperty]
        private bool _spindleSpeedEnabled;

        [ObservableProperty]
        private int _spindleSpeedRpm;

        [ObservableProperty]
        private bool _spindleStartEnabled;

        [ObservableProperty]
        private string _spindleStartCommand;

        [ObservableProperty]
        private bool _spindleStopEnabled;

        [ObservableProperty]
        private bool _spindleDelayEnabled;

        [ObservableProperty]
        private double _spindleDelaySeconds;

        [ObservableProperty]
        private bool _coolantControlEnabled;

        [ObservableProperty]
        private bool _coolantStartEnabled;

        [ObservableProperty]
        private bool _coolantStopEnabled;

        [ObservableProperty]
        private bool _addStartPosition;

        [ObservableProperty]
        private double _startX;

        [ObservableProperty]
        private double _startY;

        [ObservableProperty]
        private double _startZ;

        [ObservableProperty]
        private bool _addEndPosition;

        [ObservableProperty]
        private double _endX;

        [ObservableProperty]
        private double _endY;

        [ObservableProperty]
        private double _endZ;

        [ObservableProperty]
        private bool _setWorkCoordinateSystem;

        [ObservableProperty]
        private string _workCoordinateSystem;

        /// <summary>OK: сохранить настройки и закрыть окно.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Отмена: закрыть окно, не меняя настроек.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Смена темы видна сразу — окно показывает то, что получит
        /// приложение. Если настройки не приняты, тема возвращается к
        /// исходной при закрытии.
        /// </summary>
        partial void OnUseDarkThemeChanged(bool value)
        {
            _themeService?.ApplyTheme(value);
        }

        /// <summary>
        /// Закрытие без OK (отмена, крестик, Esc) отменяет и предпросмотр
        /// темы: настройки не сохранены, значит и вид приложения меняться
        /// не должен.
        /// </summary>
        public override void OnClosed()
        {
            base.OnClosed();

            if (!_isAccepted)
                UseDarkTheme = _initialDarkTheme;
        }

        private void OnOk()
        {
            ApplyToSettings(_settings);
            _settingsStore?.Save();
            _isAccepted = true;
            RequestClose();
        }

        /// <summary>Читает настройки в свойства окна по таблице маппинга.</summary>
        private void LoadFromSettings(GCodeSettings settings)
        {
            foreach (var (path, _) in SettingsMapping.Entries)
                EditorProperty(path).SetValue(this, SettingsMapping.GetValue(settings, path));

            if (string.IsNullOrEmpty(WorkCoordinateSystem))
                WorkCoordinateSystem = DefaultWorkCoordinateSystem;
        }

        /// <summary>Сохраняет свойства окна в настройки по той же таблице.</summary>
        private void ApplyToSettings(GCodeSettings settings)
        {
            foreach (var (path, _) in SettingsMapping.Entries)
                SettingsMapping.SetValue(settings, path, EditorProperty(path).GetValue(this));

            // Legacy-поведение: пустой WCS трактуется как G54.
            if (string.IsNullOrEmpty(settings.WorkCoordinate.WorkCoordinateSystem))
                settings.WorkCoordinate.WorkCoordinateSystem = DefaultWorkCoordinateSystem;
        }

        /// <summary>
        /// Свойство окна для записи таблицы: имя совпадает с последним звеном
        /// пути настройки (например, «Spindle.SpindleSpeedRpm» —
        /// <see cref="SpindleSpeedRpm"/>). Полнота соответствия проверяется
        /// тестом, поэтому пропущенный параметр не доживёт до окна.
        /// </summary>
        internal static PropertyInfo EditorProperty(string path)
            => typeof(SettingsViewModel).GetProperty(
                path[(path.LastIndexOf('.') + 1)..],
                BindingFlags.Public | BindingFlags.Instance);
    }
}
