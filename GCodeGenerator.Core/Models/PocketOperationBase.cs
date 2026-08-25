using GCodeGenerator.GCodeGenerators.Interfaces;

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
    public abstract class PocketOperationBase : MillingOperationBase, IPocketOperation
    {
        protected PocketOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
        {
        }

        /// <summary>Как инструмент обходит карман: по спирали или строками.</summary>
        public PocketStrategy PocketStrategy { get; set; } = PocketStrategy.Spiral;

        /// <summary>Шаг между проходами, % от диаметра инструмента.</summary>
        public double StepPercentOfTool { get; set; } = 40.0;

        /// <summary>Угол строк для стратегии Lines, градусы к оси X.</summary>
        public double LineAngleDeg { get; set; } = 0.0;

        /// <summary>
        /// Уклон стенки, градусы (0 — вертикально). Положительные значения
        /// сужают карман книзу.
        /// </summary>
        public double WallTaperAngleDeg { get; set; } = 0.0;

        /// <summary>Выполнять черновой проход с припуском.</summary>
        public bool IsRoughingEnabled { get; set; }

        /// <summary>Выполнять чистовой проход по припуску.</summary>
        public bool IsFinishingEnabled { get; set; }

        /// <summary>Припуск на обработку, мм: по контуру и по глубине.</summary>
        public double FinishAllowance { get; set; } = 0.0;

        /// <summary>Что снимает чистовой проход: стенки, дно или всё.</summary>
        public PocketFinishingMode FinishingMode { get; set; } = PocketFinishingMode.All;
    }
}
