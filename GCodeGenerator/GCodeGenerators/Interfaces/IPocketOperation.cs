using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Interfaces
{
    /// <summary>
    /// Интерфейс для операций фрезерования карманов.
    /// Определяет общие свойства всех типов карманов.
    /// </summary>
    public interface IPocketOperation
    {
        /// <summary>
        /// Включена ли черновая обработка (с припуском).
        /// </summary>
        bool IsRoughingEnabled { get; set; }

        /// <summary>
        /// Включена ли чистовая обработка (с припуском).
        /// </summary>
        bool IsFinishingEnabled { get; set; }

        /// <summary>
        /// Припуск на обработку (мм), используется по контуру и по глубине.
        /// </summary>
        double FinishAllowance { get; set; }

        /// <summary>
        /// Режим чистовой обработки.
        /// </summary>
        PocketFinishingMode FinishingMode { get; set; }

        /// <summary>
        /// Стратегия обработки кармана.
        /// </summary>
        PocketStrategy PocketStrategy { get; set; }

        /// <summary>
        /// Направление фрезерования.
        /// </summary>
        MillingDirection Direction { get; set; }

        /// <summary>
        /// Общая глубина обработки.
        /// </summary>
        double TotalDepth { get; set; }

        /// <summary>
        /// Глубина за один проход.
        /// </summary>
        double StepDepth { get; set; }

        /// <summary>
        /// Диаметр инструмента.
        /// </summary>
        double ToolDiameter { get; set; }

        /// <summary>
        /// Высота контура (начальная Z координата).
        /// </summary>
        double ContourHeight { get; set; }

        /// <summary>
        /// Скорость быстрого перемещения в плоскости XY.
        /// </summary>
        double FeedXYRapid { get; set; }

        /// <summary>
        /// Скорость рабочей подачи в плоскости XY.
        /// </summary>
        double FeedXYWork { get; set; }

        /// <summary>
        /// Скорость быстрого перемещения по оси Z.
        /// </summary>
        double FeedZRapid { get; set; }

        /// <summary>
        /// Скорость рабочей подачи по оси Z.
        /// </summary>
        double FeedZWork { get; set; }

        /// <summary>
        /// Безопасная высота Z для перемещений.
        /// </summary>
        double SafeZHeight { get; set; }

        /// <summary>
        /// Высота отвода между проходами.
        /// </summary>
        double RetractHeight { get; set; }

        /// <summary>
        /// Шаг обработки как процент от диаметра инструмента.
        /// </summary>
        double StepPercentOfTool { get; set; }

        /// <summary>
        /// Угол линий для стратегии Lines (градусы к оси X).
        /// </summary>
        double LineAngleDeg { get; set; }

        /// <summary>
        /// Уклон стенки, градусы. Диапазон [0; 90). 0 – вертикально. Положительные значения дают сужение внутрь к низу.
        /// </summary>
        double WallTaperAngleDeg { get; set; }

        /// <summary>
        /// Количество знаков после запятой для координат.
        /// </summary>
        int Decimals { get; set; }
    }
}

