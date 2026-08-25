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
        protected PocketOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
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
        /// Уклон стенки, градусы (0 — вертикально). Положительные значения
        /// сужают карман книзу.
        /// </summary>
        [ObservableProperty]
        private double _wallTaperAngleDeg = 0.0;

        /// <summary>Выполнять черновой проход с припуском.</summary>
        [ObservableProperty]
        private bool _isRoughingEnabled;

        /// <summary>Выполнять чистовой проход по припуску.</summary>
        [ObservableProperty]
        private bool _isFinishingEnabled;

        /// <summary>Припуск на обработку, мм: по контуру и по глубине.</summary>
        [ObservableProperty]
        private double _finishAllowance = 0.0;

        /// <summary>Что снимает чистовой проход: стенки, дно или всё.</summary>
        [ObservableProperty]
        private PocketFinishingMode _finishingMode = PocketFinishingMode.All;
    }
}
