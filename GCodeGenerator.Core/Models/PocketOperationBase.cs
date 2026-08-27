#nullable enable
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общая часть операций выборки кармана: подвод инструмента, стратегия
    /// обхода, шаг между проходами, уклон стенки и черновой/чистовой проход
    /// с припуском.
    ///
    /// Эти параметры описывают не форму кармана, а способ снятия материала,
    /// поэтому одинаковы для окружности, эллипса, прямоугольника и контура из
    /// чертежа. Раньше каждая модель объявляла их заново — восемь свойств в
    /// четырёх экземплярах, и новый параметр приходилось добавлять во все.
    /// </summary>
    public abstract partial class PocketOperationBase : MillingOperationBase
    {
        /// <summary>
        /// Защитный предел числа оборотов винтового входа на один слой.
        /// Тысяча оборотов уже означает практически нулевой угол или диаметр
        /// и дала бы тысячи кадров до начала самой обработки.
        /// </summary>
        public const int MaxHelicalEntryTurnsPerLayer = 1000;

        protected PocketOperationBase(OperationCategory category, string name)
            : base(category, name)
        {
        }

        /// <summary>
        /// Обычный карман или остров — необрабатываемая геометрия, которую
        /// остальные операции карманов должны обходить. Обычный режим остаётся
        /// значением по умолчанию для совместимости со старыми проектами.
        /// </summary>
        [ObservableProperty]
        private PocketMode _pocketMode = PocketMode.Machining;

        /// <summary>Вход в слой вертикально или по винтовой траектории.</summary>
        [ObservableProperty]
        private PocketEntryMode _entryMode = PocketEntryMode.Vertical;

        /// <summary>
        /// Угол винтового подвода к плоскости XY, градусы. Чем меньше угол,
        /// тем больше оборотов инструмент делает на ту же глубину.
        /// </summary>
        [ObservableProperty]
        private double _entryAngle = 5.0;

        /// <summary>
        /// Диаметр окружности, по которой движется центр фрезы при винтовом
        /// подводе, мм. Диаметр самой фрезы учитывается отдельно при проверке
        /// вписывания траектории в карман.
        /// </summary>
        [ObservableProperty]
        private double _helicalEntryDiameter = 3.0;

        /// <summary>Как инструмент обходит карман: по спирали или строками.</summary>
        [ObservableProperty]
        private PocketStrategy _pocketStrategy = PocketStrategy.Spiral;

        // null означает историческое направление выбранной стратегии:
        // спираль из центра наружу, концентрические проходы снаружи внутрь.
        // Так старые проекты без нового поля сохраняют прежний G-code.
        private PocketProcessingDirection? _processingDirectionSetting;

        /// <summary>
        /// Направление обработки для спиральной и концентрической стратегий.
        /// Пока пользователь не сделал явный выбор, возвращается прежнее
        /// направление конкретной стратегии.
        /// </summary>
        [JsonIgnore]
        public PocketProcessingDirection ProcessingDirection
        {
            get => _processingDirectionSetting
                   ?? (PocketStrategy == PocketStrategy.Concentric
                       ? PocketProcessingDirection.OutsideIn
                       : PocketProcessingDirection.CenterOutward);
            set
            {
                if (SetProperty(ref _processingDirectionSetting, value, nameof(ProcessingDirectionSetting)))
                    OnPropertyChanged(nameof(ProcessingDirection));
            }
        }

        /// <summary>
        /// Сохраняемое явное значение. null не записывается: отсутствие поля
        /// в старом проекте и неизменённая новая операция эквивалентны.
        /// </summary>
        [JsonPropertyName("ProcessingDirection")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PocketProcessingDirection? ProcessingDirectionSetting
        {
            get => _processingDirectionSetting;
            set
            {
                if (SetProperty(ref _processingDirectionSetting, value))
                    OnPropertyChanged(nameof(ProcessingDirection));
            }
        }

        partial void OnPocketStrategyChanged(PocketStrategy value)
        {
            // У неявного значения направление зависит от стратегии.
            if (!_processingDirectionSetting.HasValue)
                OnPropertyChanged(nameof(ProcessingDirection));
        }

        /// <summary>Шаг между проходами, % от диаметра инструмента.</summary>
        [ObservableProperty]
        private double _stepPercentOfTool = 40.0;

        /// <summary>Угол строк для стратегии Lines, градусы к оси X.</summary>
        [ObservableProperty]
        private double _lineAngleDeg = 0.0;

        /// <summary>
        /// Наибольший уклон стенки. При 90 градусах стенка становится
        /// горизонтальной, а смещение контура обращается в бесконечность.
        /// </summary>
        private const double MaxWallTaperAngleDeg = 89.999999;

        private double _wallTaperAngleDeg;
        private bool _isRoughingEnabled;
        private bool _isFinishingEnabled;

        /// <summary>
        /// Уклон стенки, градусы (0 — вертикально). Положительные значения
        /// сужают карман книзу. Значение вне диапазона заменяется ближайшим
        /// допустимым: ограничение принадлежит самой операции, а не окну —
        /// нарушить его может и файл проекта.
        /// </summary>
        public double WallTaperAngleDeg
        {
            get => _wallTaperAngleDeg;
            set => SetProperty(ref _wallTaperAngleDeg,
                value < 0 ? 0 : value > MaxWallTaperAngleDeg ? MaxWallTaperAngleDeg : value);
        }

        /// <summary>
        /// Выполнять черновой проход с припуском. Вместе с
        /// <see cref="IsFinishingEnabled"/> даёт полный цикл: сначала выборка
        /// с припуском, затем его снятие — планировщик проходов поддерживает
        /// такое сочетание, поэтому запрета здесь нет.
        /// </summary>
        public bool IsRoughingEnabled
        {
            get => _isRoughingEnabled;
            set => SetProperty(ref _isRoughingEnabled, value);
        }

        /// <summary>Выполнять чистовой проход по припуску.</summary>
        public bool IsFinishingEnabled
        {
            get => _isFinishingEnabled;
            set => SetProperty(ref _isFinishingEnabled, value);
        }

        /// <summary>
        /// Припуск на обработку, мм: по контуру и по глубине.
        ///
        /// Значение по умолчанию ненулевое: чистовой проход снимает именно
        /// припуск, и включить его при нулевом было нельзя — операция сразу
        /// становилась негодной. Пока ни черновой, ни чистовой проход не
        /// включён, припуск в расчёт не идёт и на программу не влияет.
        /// </summary>
        [ObservableProperty]
        private double _finishAllowance = DefaultFinishAllowance;

        /// <summary>Припуск по умолчанию, мм.</summary>
        public const double DefaultFinishAllowance = 0.2;

        /// <summary>Что снимает чистовой проход: стенки, дно или всё.</summary>
        [ObservableProperty]
        private PocketFinishingMode _finishingMode = PocketFinishingMode.All;
    }
}
