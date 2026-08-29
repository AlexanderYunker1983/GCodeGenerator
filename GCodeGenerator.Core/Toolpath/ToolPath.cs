#nullable enable
using System;
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
    public class ToolMove : ToolPathItem
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
            // Дуга представима только типом ArcMove, который требует все
            // пять величин конструктором: кадр G2/G3 без конечной точки,
            // смещения центра или подачи не имеет смысла. Прямое создание
            // ToolMove с дуговым видом — ошибка кода, и она называется
            // здесь, при создании, а не при выводе программы.
            if ((kind == ToolMoveKind.ArcClockwise || kind == ToolMoveKind.ArcCounterClockwise)
                && GetType() == typeof(ToolMove))
            {
                throw new ArgumentException(
                    "Дуга описывается типом ArcMove: конечная точка, смещение центра и подача обязательны.",
                    nameof(kind));
            }

            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            RequireFinite(z, nameof(z));
            RequireFinite(centerOffsetX, nameof(centerOffsetX));
            RequireFinite(centerOffsetY, nameof(centerOffsetY));
            RequireFinite(feed, nameof(feed));

            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            CenterOffsetX = centerOffsetX;
            CenterOffsetY = centerOffsetY;
            Feed = feed;
        }

        private static void RequireFinite(double? value, string parameter)
        {
            if (value.HasValue && !double.IsFinite(value.Value))
                throw new ArgumentOutOfRangeException(parameter, value, "Tool-path values must be finite.");
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
    /// Дуга траектории. Пять величин обязательны — кадр G2/G3 без любой
    /// из них не имеет смысла, — а необязательная конечная Z превращает
    /// плоскую дугу в винтовое перемещение. Обязательность обеспечивает конструктор:
    /// нелегальная дуга непредставима, и ни постпроцессору, ни превью
    /// не приходится перепроверять её при выводе. Прежде обязательность
    /// восстанавливали проверки постпроцессора; отказ теперь приходит
    /// раньше — в месте, где дугу собрали.
    /// </summary>
    public sealed class ArcMove : ToolMove
    {
        public ArcMove(
            bool clockwise,
            double x,
            double y,
            double centerOffsetX,
            double centerOffsetY,
            double feed,
            double? z = null)
            : base(
                clockwise ? ToolMoveKind.ArcClockwise : ToolMoveKind.ArcCounterClockwise,
                x, y, z, centerOffsetX, centerOffsetY, feed)
        {
            // Пять величин хранятся и как необязательные слова базового
            // перемещения (их читает общий вывод), и как собственные
            // обязательные числа дуги: обязательность выражена типом,
            // без утверждений о непустоте при чтении.
            EndX = x;
            EndY = y;
            ArcCenterOffsetX = centerOffsetX;
            ArcCenterOffsetY = centerOffsetY;
            ArcFeed = feed;
            EndZ = z;
        }

        /// <summary>Конечная точка дуги, X.</summary>
        public double EndX { get; }

        /// <summary>Конечная точка дуги, Y.</summary>
        public double EndY { get; }

        /// <summary>Смещение центра от начала дуги по X (слово I).</summary>
        public double ArcCenterOffsetX { get; }

        /// <summary>Смещение центра от начала дуги по Y (слово J).</summary>
        public double ArcCenterOffsetY { get; }

        /// <summary>Подача дуги, мм/мин.</summary>
        public double ArcFeed { get; }

        /// <summary>
        /// Конечная Z винтового перемещения; <c>null</c> у плоской дуги.
        /// </summary>
        public double? EndZ { get; }
    }

    /// <summary>
    /// Траектория одной операции: её перемещения и точность вывода координат.
    /// </summary>
    public sealed class ToolPathOperation
    {
        public ToolPathOperation(
            string name,
            string description,
            int decimals,
            object? source = null,
            int sourceIndex = -1)
        {
            if (sourceIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));

            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Decimals = decimals;
            Source = source;
            SourceIndex = sourceIndex;
        }

        /// <summary>
        /// Операция, породившая эту траекторию; null — траектория собрана
        /// без неё (тесты построителя). Нужна предпросмотру: он подсвечивает
        /// выбранную операцию и открывает её по двойному щелчку, поэтому
        /// обязан знать, чей участок траектории показывает.
        /// </summary>
        public object? Source { get; }

        /// <summary>
        /// Место исходной операции в проекте; -1 у вручную собранных
        /// траекторий. Нужна постпроверкам, чтобы назвать опасную операцию.
        /// </summary>
        public int SourceIndex { get; }

        /// <summary>Имя операции, заданное пользователем.</summary>
        public string Name { get; }

        /// <summary>Короткое описание операции для комментария.</summary>
        public string Description { get; }

        /// <summary>Знаков после запятой у координат этой операции.</summary>
        public int Decimals { get; }

        private readonly List<ToolPathItem> _items = new List<ToolPathItem>();

        /// <summary>
        /// Перемещения и пояснения по порядку — только чтение: траекторию
        /// наполняет <see cref="ToolPathBuilder"/>, а потребители смотрят
        /// на готовую и менять её не должны.
        /// </summary>
        public IReadOnlyList<ToolPathItem> Items => _items;

        /// <summary>Добавляет участок траектории (для построителя).</summary>
        internal void Add(ToolPathItem item) => _items.Add(item);
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
    /// постпроцессор, а оба рабочих предпросмотра показывают уже программу —
    /// с её координатным прологом, округлением и эпилогом.
    /// </summary>
    public sealed class ToolPath
    {
        private readonly List<ToolPathOperation> _operations = new List<ToolPathOperation>();

        /// <summary>Операции по порядку обработки — только чтение.</summary>
        public IReadOnlyList<ToolPathOperation> Operations => _operations;

        /// <summary>Добавляет траекторию операции в порядок обработки.</summary>
        public void AddOperation(ToolPathOperation operation)
            => _operations.Add(operation ?? throw new ArgumentNullException(nameof(operation)));

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
