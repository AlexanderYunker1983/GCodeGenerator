#nullable enable
using GCodeGenerator.GCodeGenerators.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общая часть операций выборки кармана: стратегия обхода, шаг между
    /// проходами, уклон стенки и черновой/чистовой проход с припуском.
    ///
    /// Эти параметры описывают не форму кармана, а способ снятия материала,
    /// поэтому одинаковы для окружности, эллипса, прямоугольника и контура из
    /// чертежа. Раньше каждая модель объявляла их заново — восемь свойств в
    /// четырёх экземплярах, и новый параметр приходилось добавлять во все.
    /// </summary>
    public abstract partial class PocketOperationBase : MillingOperationBase, IPocketOperation
    {
        protected PocketOperationBase(OperationCategory category, string name)
            : base(category, name)
        {
        }

        /// <summary>Как инструмент обходит карман: по спирали или строками.</summary>
        [ObservableProperty]
        private PocketStrategy _pocketStrategy = PocketStrategy.Spiral;

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
