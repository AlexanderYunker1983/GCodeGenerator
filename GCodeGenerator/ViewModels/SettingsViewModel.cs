#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
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
        private readonly ISettingsStore? _settingsStore;
        private readonly IThemeService? _themeService;
        private readonly ILocalizationManager? _localizationManager;

        /// <summary>Язык на момент открытия окна — к нему возвращает отмена.</summary>
        private readonly string _initialLanguage;

        /// <summary>Тема на момент открытия окна — к ней возвращает отмена.</summary>
        private readonly bool _initialDarkTheme;

        private bool _isAccepted;

        /// <summary>
        /// Идёт заполнение окна сохранёнными значениями. Предпросмотр языка
        /// и темы в это время не нужен: показывать «изменение» на то, что
        /// уже действует, — значит менять язык при каждом открытии окна.
        /// </summary>
        private bool _isLoading;

        public SettingsViewModel()
            : this(null, null, null)
        {
        }

        public SettingsViewModel(ILocalizationManager? localizationManager, ISettingsStore? settingsStore, IThemeService? themeService)
            : this(localizationManager, settingsStore, themeService, null)
        {
        }

        public SettingsViewModel(
            ILocalizationManager? localizationManager,
            ISettingsStore? settingsStore,
            IThemeService? themeService,
            IPostProcessorRegistry? postProcessors)
        {
            // Список стоек берётся из того же реестра, по которому генерация
            // выбирает постпроцессор: окно не может предложить стойку,
            // которую генерация отвергнет.
            PostProcessors = (postProcessors ?? new PostProcessorRegistry()).All;

            // Настройки и тема поступают через IoC. Безаргументный конструктор —
            // для XAML-дизайнера: фолбэк на настройки по умолчанию.
            _settings = settingsStore?.Current ?? new GCodeSettings();
            _settingsStore = settingsStore;
            _themeService = themeService;
            _localizationManager = localizationManager;

            // Пункт 8.3: без захардкоженного фолбэка — отсутствующий ключ
            // вернёт «?Key?» (лог — в LocalizationManager).
            DisplayName = localizationManager?.GetString("GCodeSettingsTitle") ?? "GCodeSettingsTitle";

            OkCommand = new RelayCommand(OnOk);
            CancelCommand = new RelayCommand(RequestClose);
            SaveAsDefaultsCommand = new RelayCommand(OnSaveAsDefaults);

            LoadFromSettings(_settings);
            _initialDarkTheme = UseDarkTheme;
            _initialLanguage = Language;
        }

        [ObservableProperty]
        private string _displayName = string.Empty;

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
        private string _postProcessorName = string.Empty;

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
        private string _spindleStartCommand = string.Empty;

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
        private string _workCoordinateSystem = string.Empty;

        /// <summary>Код языка интерфейса; пустая строка — язык системы.</summary>
        [ObservableProperty]
        private string _language = string.Empty;

        /// <summary>
        /// Языки, между которыми выбирает пользователь. Пустой код означает
        /// язык системы — он и стоит по умолчанию.
        /// </summary>
        public IReadOnlyList<LanguageChoice> Languages { get; } = LanguageChoice.All;

        /// <summary>
        /// Стойки, для которых продукт умеет строить программу. Показывается
        /// название, сохраняется ключ (<see cref="IPostProcessor.Key"/>).
        /// </summary>
        public IReadOnlyList<IPostProcessor> PostProcessors { get; }

        /// <summary>OK: сохранить настройки и закрыть окно.</summary>
        public ICommand OkCommand { get; }

        /// <summary>Отмена: закрыть окно, не меняя настроек.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Записать значения окна как умолчания генерации для новых проектов.
        /// Текущий документ не меняется — его настройки меняет OK.
        /// </summary>
        public ICommand SaveAsDefaultsCommand { get; }

        /// <summary>
        /// Смена темы видна сразу — окно показывает то, что получит
        /// приложение. Если настройки не приняты, тема возвращается к
        /// исходной при закрытии.
        /// </summary>
        partial void OnUseDarkThemeChanged(bool value)
        {
            if (_isLoading)
                return;

            _themeService?.ApplyTheme(value);
        }

        /// <summary>
        /// Смена языка тоже видна сразу: надписи в окнах перечитываются на
        /// месте. Как и тема, это предпросмотр — отмена возвращает прежний
        /// язык, чтобы вид программы не расходился с сохранёнными настройками.
        /// </summary>
        partial void OnLanguageChanged(string value)
        {
            if (_isLoading)
                return;

            _localizationManager?.ChangeCulture(LanguageChoice.ToCulture(value));
        }

        /// <summary>
        /// Закрытие без OK (отмена, крестик, Esc) отменяет и предпросмотр
        /// темы: настройки не сохранены, значит и вид приложения меняться
        /// не должен.
        /// </summary>
        public override void OnClosed()
        {
            base.OnClosed();

            if (_isAccepted)
                return;

            UseDarkTheme = _initialDarkTheme;
            Language = _initialLanguage;
        }

        private void OnOk()
        {
            ApplyToSettings(_settings);
            _settingsStore?.Save();
            _isAccepted = true;
            RequestClose();
        }

        /// <summary>
        /// Значения окна становятся умолчаниями для новых проектов. Настройки
        /// открытого документа не трогаются: пользователь мог нажать кнопку
        /// и затем отменить окно — документ обязан остаться прежним.
        /// </summary>
        private void OnSaveAsDefaults()
        {
            var defaults = new GCodeSettings();
            ApplyToSettings(defaults);
            _settingsStore?.SaveGenerationDefaults(defaults);
        }

        /// <summary>Читает настройки в свойства окна по таблице маппинга.</summary>
        private void LoadFromSettings(GCodeSettings settings)
        {
            _isLoading = true;
            try
            {
                foreach (var (path, _) in SettingsMapping.Entries)
                    EditorProperty(path).SetValue(this, SettingsMapping.GetValue(settings, path));

                if (string.IsNullOrEmpty(WorkCoordinateSystem))
                    WorkCoordinateSystem = DefaultWorkCoordinateSystem;
            }
            finally
            {
                _isLoading = false;
            }
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
        {
            var name = path[(path.LastIndexOf('.') + 1)..];

            // Отсутствие свойства — ошибка самой таблицы: она перечисляет
            // настройки вручную, и опечатка иначе дала бы отказ без указания,
            // какая строка виновата. Текст адресован разработчику, поэтому он
            // английский, как остальные внутренние отказы: кириллица в строках
            // view-моделей запрещена проверкой CI.
            return typeof(SettingsViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Settings entry '{path}': the settings window has no property named '{name}'.");
        }
    }
}
