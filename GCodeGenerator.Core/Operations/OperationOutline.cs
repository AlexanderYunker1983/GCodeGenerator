using System.Collections.Generic;

namespace GCodeGenerator.Operations
{
    /// <summary>Чем является набор точек контура операции.</summary>
    public enum OperationOutlineKind
    {
        /// <summary>Отдельные точки — центры отверстий.</summary>
        Points,

        /// <summary>Ломаная или замкнутый контур.</summary>
        Contour
    }

    /// <summary>
    /// Очертание операции: то, как она выглядит на плоскости до выбора
    /// инструмента и стратегии.
    ///
    /// Очертание не зависит ни от способа рисования, ни от того, кто
    /// спрашивает: схема операций рисует его линиями, но им же можно
    /// посчитать габариты заготовки или выгрузить контур в чертёж.
    /// Поэтому здесь нет ни цветов, ни толщин — только точки и то,
    /// чем они являются.
    /// </summary>
    public sealed class OperationOutline
    {
        public OperationOutline(
            OperationOutlineKind kind,
            IReadOnlyList<(double X, double Y)> points,
            bool isArea)
        {
            Kind = kind;
            Points = points;
            IsArea = isArea;
        }

        /// <summary>Точки или контур.</summary>
        public OperationOutlineKind Kind { get; }

        /// <summary>Точки в координатах программы, мм.</summary>
        public IReadOnlyList<(double X, double Y)> Points { get; }

        /// <summary>
        /// Контур ограничивает область выборки материала (карман), а не
        /// линию обхода: рисуется заливкой, а не только линией.
        /// </summary>
        public bool IsArea { get; }
    }
}
