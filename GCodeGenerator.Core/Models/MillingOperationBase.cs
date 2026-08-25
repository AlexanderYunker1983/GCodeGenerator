using System.Text.Json.Serialization;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Подачи операции, мм/мин.
    /// </summary>
    public sealed class Feeds
    {
        /// <summary>Быстрое перемещение в плоскости XY.</summary>
        public double XYRapid { get; set; } = 1000.0;

        /// <summary>Рабочая подача в плоскости XY.</summary>
        public double XYWork { get; set; } = 300.0;

        /// <summary>Быстрое перемещение по оси Z.</summary>
        public double ZRapid { get; set; } = 500.0;

        /// <summary>Рабочая подача по оси Z.</summary>
        public double ZWork { get; set; } = 200.0;
    }

    /// <summary>
    /// Раскладка обработки по глубине и безопасные высоты, мм.
    /// </summary>
    public sealed class DepthPlan
    {
        /// <summary>Высота контура — Z, с которой начинается обработка.</summary>
        public double ContourHeight { get; set; } = 0.0;

        /// <summary>Полная глубина обработки.</summary>
        public double TotalDepth { get; set; } = 2.0;

        /// <summary>Глубина за один проход.</summary>
        public double StepDepth { get; set; } = 1.0;

        /// <summary>Безопасная высота для перемещений над заготовкой.</summary>
        public double SafeZHeight { get; set; } = 1.0;

        /// <summary>Высота отвода между проходами.</summary>
        public double RetractHeight { get; set; } = 0.3;
    }

    /// <summary>
    /// Общая часть фрезерных операций: инструмент, подачи, раскладка по глубине,
    /// направление обхода и точность вывода координат.
    ///
    /// Эти параметры есть у каждой операции профиля и кармана. Раньше все
    /// пятнадцать объявлялись заново в каждой из десяти моделей, а также
    /// в двух интерфейсах, поэтому добавление одного параметра резания
    /// означало правку в десятках мест.
    ///
    /// Значения сгруппированы по смыслу (<see cref="Feeds"/>,
    /// <see cref="DepthPlan"/>), но остаются доступны и по отдельности:
    /// диалоги и привязки интерфейса работают с плоскими свойствами, и файл
    /// проекта тоже сохраняет их плоско — формат файла не обязан повторять
    /// внутреннюю структуру модели, а его совместимость важнее.
    /// </summary>
    public abstract class MillingOperationBase : OperationBase
    {
        protected MillingOperationBase(OperationType type, OperationCategory category, string name)
            : base(type, category, name)
        {
        }

        /// <summary>Подачи операции. В файл проекта пишутся плоскими свойствами.</summary>
        [JsonIgnore]
        public Feeds Feeds { get; set; } = new Feeds();

        /// <summary>Раскладка по глубине. В файл проекта пишется плоскими свойствами.</summary>
        [JsonIgnore]
        public DepthPlan Depth { get; set; } = new DepthPlan();

        /// <summary>Направление фрезерования.</summary>
        public MillingDirection Direction { get; set; } = MillingDirection.Clockwise;

        /// <summary>Диаметр инструмента, мм.</summary>
        public double ToolDiameter { get; set; } = 3.0;

        /// <summary>Количество знаков после запятой для координат.</summary>
        public int Decimals { get; set; } = 3;

        // --- Подачи (плоские имена для интерфейса и файла проекта) ----------

        /// <inheritdoc cref="Models.Feeds.XYRapid"/>
        public double FeedXYRapid
        {
            get => Feeds.XYRapid;
            set => Feeds.XYRapid = value;
        }

        /// <inheritdoc cref="Models.Feeds.XYWork"/>
        public double FeedXYWork
        {
            get => Feeds.XYWork;
            set => Feeds.XYWork = value;
        }

        /// <inheritdoc cref="Models.Feeds.ZRapid"/>
        public double FeedZRapid
        {
            get => Feeds.ZRapid;
            set => Feeds.ZRapid = value;
        }

        /// <inheritdoc cref="Models.Feeds.ZWork"/>
        public double FeedZWork
        {
            get => Feeds.ZWork;
            set => Feeds.ZWork = value;
        }

        // --- Глубина и высоты ------------------------------------------------

        /// <inheritdoc cref="DepthPlan.ContourHeight"/>
        public double ContourHeight
        {
            get => Depth.ContourHeight;
            set => Depth.ContourHeight = value;
        }

        /// <inheritdoc cref="DepthPlan.TotalDepth"/>
        public double TotalDepth
        {
            get => Depth.TotalDepth;
            set => Depth.TotalDepth = value;
        }

        /// <inheritdoc cref="DepthPlan.StepDepth"/>
        public double StepDepth
        {
            get => Depth.StepDepth;
            set => Depth.StepDepth = value;
        }

        /// <inheritdoc cref="DepthPlan.SafeZHeight"/>
        public double SafeZHeight
        {
            get => Depth.SafeZHeight;
            set => Depth.SafeZHeight = value;
        }

        /// <inheritdoc cref="DepthPlan.RetractHeight"/>
        public double RetractHeight
        {
            get => Depth.RetractHeight;
            set => Depth.RetractHeight = value;
        }
    }
}
