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
    ///
    /// Подачи, глубина слоя и точность вывода общие у фрезеровки и сверления
    /// и живут в <see cref="CuttingOperationBase"/>; здесь остаётся то, чем
    /// фрезеровка отличается: инструмент, направление обхода и высоты,
    /// от которых считается траектория вокруг контура.
    /// </summary>
    public abstract partial class MillingOperationBase : CuttingOperationBase
    {
        protected MillingOperationBase(OperationCategory category, string name)
            : base(category, name)
        {
        }

        /// <summary>Направление фрезерования.</summary>
        [ObservableProperty]
        private MillingDirection _direction = MillingDirection.Clockwise;

        /// <summary>Диаметр инструмента, мм.</summary>
        [ObservableProperty]
        private double _toolDiameter = 3.0;

        // --- Глубина и высоты, мм --------------------------------------------

        /// <summary>Высота контура — Z, с которой начинается обработка.</summary>
        [ObservableProperty]
        private double _contourHeight;

        /// <summary>Безопасная высота для перемещений над заготовкой.</summary>
        [ObservableProperty]
        private double _safeZHeight = 1.0;
    }
}
