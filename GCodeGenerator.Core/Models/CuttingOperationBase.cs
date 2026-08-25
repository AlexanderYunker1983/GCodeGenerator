using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общая часть операций резания: подачи, раскладка по глубине, отвод
    /// и точность вывода координат.
    ///
    /// Эти параметры описывают не способ обработки, а сам процесс резания,
    /// поэтому одинаковы у фрезеровки и у сверления: инструмент подходит на
    /// быстрой подаче, врезается на рабочей, снимает материал слоями заданной
    /// глубины и отводится на заданную высоту. Раньше они объявлялись дважды —
    /// в основе фрезерных операций и заново в сверлении, — с одинаковыми
    /// именами, одинаковыми значениями по умолчанию и раздельными проверками:
    /// правило, добавленное для фрезеровки, до сверления не доходило.
    ///
    /// Отдельный тип вместо общего предка «сверление — частный случай
    /// фрезеровки» выбран намеренно: у сверления нет ни направления обхода,
    /// ни диаметра инструмента как параметра траектории — оно идёт по оси,
    /// а не вокруг контура.
    /// </summary>
    public abstract partial class CuttingOperationBase : OperationBase
    {
        protected CuttingOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
        {
        }

        /// <summary>Количество знаков после запятой для координат.</summary>
        [ObservableProperty]
        private int _decimals = 3;

        // --- Подачи, мм/мин --------------------------------------------------

        /// <summary>Быстрое перемещение в плоскости XY.</summary>
        [ObservableProperty]
        private double _feedXYRapid = 1000.0;

        /// <summary>Рабочая подача в плоскости XY.</summary>
        [ObservableProperty]
        private double _feedXYWork = 300.0;

        /// <summary>Быстрое перемещение по оси Z.</summary>
        [ObservableProperty]
        private double _feedZRapid = 500.0;

        /// <summary>Рабочая подача по оси Z.</summary>
        [ObservableProperty]
        private double _feedZWork = 200.0;

        // --- Глубина и высоты, мм --------------------------------------------

        /// <summary>Полная глубина обработки.</summary>
        [ObservableProperty]
        private double _totalDepth = 2.0;

        /// <summary>Глубина за один проход.</summary>
        [ObservableProperty]
        private double _stepDepth = 1.0;

        /// <summary>Высота отвода между проходами.</summary>
        [ObservableProperty]
        private double _retractHeight = 0.3;
    }
}
