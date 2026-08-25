using System.Collections.Generic;

namespace GCodeGenerator.Toolpath
{
    /// <summary>Чем занято перемещение инструмента.</summary>
    public enum ToolMoveKind
    {
        /// <summary>Холостой ход.</summary>
        Rapid,

        /// <summary>Рабочее прямолинейное перемещение.</summary>
        Linear,

        /// <summary>Дуга по часовой стрелке.</summary>
        ArcClockwise,

        /// <summary>Дуга против часовой стрелки.</summary>
        ArcCounterClockwise
    }

    /// <summary>Часть траектории операции: перемещение или пояснение.</summary>
    public abstract class ToolPathItem
    {
    }

    /// <summary>
    /// Пояснение к траектории («проход 2, глубина −1.5»). В программу
    /// попадает комментарием, если они включены.
    /// </summary>
    public sealed class ToolPathNote : ToolPathItem
    {
        public ToolPathNote(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Text { get; }
    }

    /// <summary>
    /// Перемещение инструмента в координатах детали, миллиметры.
    ///
    /// Ось, которая не меняется, не задаётся вовсе: программа не должна
    /// выводить координату, которую станок и так удерживает.
    /// </summary>
    public sealed class ToolMove : ToolPathItem
    {
        public ToolMove(
            ToolMoveKind kind,
            double? x = null,
            double? y = null,
            double? z = null,
            double? centerOffsetX = null,
            double? centerOffsetY = null,
            double? feed = null)
        {
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            CenterOffsetX = centerOffsetX;
            CenterOffsetY = centerOffsetY;
            Feed = feed;
        }

        public ToolMoveKind Kind { get; }

        public double? X { get; }

        public double? Y { get; }

        public double? Z { get; }

        /// <summary>Смещение центра дуги по X относительно начала (слово I).</summary>
        public double? CenterOffsetX { get; }

        /// <summary>Смещение центра дуги по Y относительно начала (слово J).</summary>
        public double? CenterOffsetY { get; }

        /// <summary>Подача, мм/мин.</summary>
        public double? Feed { get; }

        /// <summary>Дуга ли это.</summary>
        public bool IsArc => Kind == ToolMoveKind.ArcClockwise || Kind == ToolMoveKind.ArcCounterClockwise;
    }

    /// <summary>
    /// Траектория одной операции: её перемещения и точность вывода координат.
    /// </summary>
    public sealed class ToolPathOperation
    {
        public ToolPathOperation(string name, string description, int decimals, object source = null)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Decimals = decimals;
            Source = source;
        }

        /// <summary>
        /// Операция, породившая эту траекторию. Нужна предпросмотру: он
        /// подсвечивает выбранную операцию и открывает её по двойному щелчку,
        /// поэтому обязан знать, чей участок траектории показывает.
        /// </summary>
        public object Source { get; }

        /// <summary>Имя операции, заданное пользователем.</summary>
        public string Name { get; }

        /// <summary>Короткое описание операции для комментария.</summary>
        public string Description { get; }

        /// <summary>Знаков после запятой у координат этой операции.</summary>
        public int Decimals { get; }

        /// <summary>Перемещения и пояснения по порядку.</summary>
        public IList<ToolPathItem> Items { get; } = new List<ToolPathItem>();
    }

    /// <summary>
    /// Траектория инструмента для всего проекта — то, что физически проделает
    /// станок, в координатах детали и без единого G-слова.
    ///
    /// Это промежуточный слой между операциями и программой: раньше его
    /// не было, и геометрия существовала в трёх независимых видах — генераторы
    /// сразу писали G-код, двумерный предпросмотр строил контуры заново прямо
    /// из моделей, а трёхмерный разбирал уже готовую программу обратно,
    /// восстанавливая по ней модальные состояния. Любое расхождение между
    /// этими тремя путями было незаметно до станка.
    ///
    /// Теперь путь один: операция строит траекторию, программу из неё делает
    /// постпроцессор, а оба предпросмотра показывают её же.
    /// </summary>
    public sealed class ToolPath
    {
        /// <summary>Операции по порядку обработки.</summary>
        public IList<ToolPathOperation> Operations { get; } = new List<ToolPathOperation>();

        /// <summary>Все перемещения подряд, без разбивки по операциям.</summary>
        public IEnumerable<ToolMove> Moves()
        {
            foreach (var operation in Operations)
            {
                foreach (var item in operation.Items)
                {
                    if (item is ToolMove move)
                        yield return move;
                }
            }
        }

        /// <summary>Есть ли в траектории хоть одно перемещение.</summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var _ in Moves())
                    return false;
                return true;
            }
        }
    }
}
