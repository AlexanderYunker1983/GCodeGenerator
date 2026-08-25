using CommunityToolkit.Mvvm.ComponentModel;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Общая часть фрезерных операций: инструмент, подачи, раскладка по глубине,
    /// направление обхода и точность вывода координат.
    ///
    /// Эти параметры есть у каждой операции профиля и кармана. Раньше все
    /// пятнадцать объявлялись заново в каждой из десяти моделей, а также
    /// в двух интерфейсах, поэтому добавление одного параметра означало
    /// правку в десятках мест.
    ///
    /// Прежде они дополнительно хранились в объектах-группах Feeds и DepthPlan,
    /// а наружу выходили плоскими свойствами-обёртками. Группами так никто и
    /// не пользовался — ни диалоги, ни файл проекта, ни генераторы, — поэтому
    /// осталось одно представление вместо двух.
    /// </summary>
    public abstract partial class MillingOperationBase : OperationBase
    {
        protected MillingOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
        {
        }

        /// <summary>Направление фрезерования.</summary>
        [ObservableProperty]
        private MillingDirection _direction = MillingDirection.Clockwise;

        /// <summary>Диаметр инструмента, мм.</summary>
        [ObservableProperty]
        private double _toolDiameter = 3.0;

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

        /// <summary>Высота контура — Z, с которой начинается обработка.</summary>
        [ObservableProperty]
        private double _contourHeight;

        /// <summary>Полная глубина обработки.</summary>
        [ObservableProperty]
        private double _totalDepth = 2.0;

        /// <summary>Глубина за один проход.</summary>
        [ObservableProperty]
        private double _stepDepth = 1.0;

        /// <summary>Безопасная высота для перемещений над заготовкой.</summary>
        [ObservableProperty]
        private double _safeZHeight = 1.0;

        /// <summary>Высота отвода между проходами.</summary>
        [ObservableProperty]
        private double _retractHeight = 0.3;
    }
}
