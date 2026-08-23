using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators.Interfaces
{
    /// <summary>
    /// Интерфейс для операций фрезерования профилей.
    /// Определяет общие свойства всех типов профилей.
    /// </summary>
    public interface IProfileOperation
    {
        /// <summary>
        /// Режим траектории инструмента: по линии, снаружи или внутри контура.
        /// </summary>
        ToolPathMode ToolPathMode { get; set; }

        /// <summary>
        /// Режим входа в материал: вертикальный или по рампе.
        /// </summary>
        EntryMode EntryMode { get; set; }

        /// <summary>
        /// Угол входа в материал (градусы) для режима рампы.
        /// </summary>
        double EntryAngle { get; set; }

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
        /// Максимальная длина сегмента для аппроксимации дуг.
        /// </summary>
        double MaxSegmentLength { get; set; }

        /// <summary>
        /// Количество знаков после запятой для координат.
        /// </summary>
        int Decimals { get; set; }
    }
}

