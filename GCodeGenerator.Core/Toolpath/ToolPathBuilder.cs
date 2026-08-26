using System;

namespace GCodeGenerator.Toolpath
{
    /// <summary>
    /// Строит траекторию операции.
    ///
    /// Генераторы описывают движение инструмента — куда и с какой подачей, —
    /// и ничего не знают ни о G-словах, ни о номерах строк, ни о том, какая
    /// стойка будет исполнять программу. Раньше они писали G-код напрямую,
    /// поэтому диалект станка был размазан по всем генераторам и стратегиям.
    ///
    /// Имена методов оставлены прежними (<c>RapidTo</c>, <c>LinearTo</c>,
    /// <c>ArcCW</c>, <c>ArcCCW</c>, <c>Comment</c>): смысл вызовов не менялся,
    /// изменился только их адресат.
    /// </summary>
    public sealed class ToolPathBuilder
    {
        private readonly ToolPathOperation _operation;

        public ToolPathBuilder(ToolPathOperation operation)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        /// <summary>Траектория, которую наполняет построитель.</summary>
        public ToolPathOperation Operation => _operation;

        /// <summary>Пояснение к следующему участку траектории.</summary>
        public void Comment(string text)
        {
            _operation.Items.Add(new ToolPathNote(text));
        }

        // Параметра точности у перемещений нет намеренно: точность вывода
        // координат — свойство всей операции (ToolPathOperation.Decimals),
        // и словами кадра её распоряжается постпроцессор. Прежде методы
        // принимали и молча игнорировали decimals, а шесть десятков вызовов
        // старательно передавали его в никуда — интерфейс обещал управлять
        // точностью каждого перемещения и обманывал.

        /// <summary>Холостой ход к заданным осям; незаданные не меняются.</summary>
        public void RapidTo(double? x = null, double? y = null, double? z = null, double? feed = null)
        {
            Add(ToolMoveKind.Rapid, x, y, z, null, null, feed);
        }

        /// <summary>Рабочее прямолинейное перемещение к заданным осям.</summary>
        public void LinearTo(double? x = null, double? y = null, double? z = null, double? feed = null)
        {
            Add(ToolMoveKind.Linear, x, y, z, null, null, feed);
        }

        /// <summary>Дуга по часовой стрелке в точку (x, y) вокруг центра (i, j).</summary>
        public void ArcCW(double x, double y, double i, double j, double feed)
        {
            Add(ToolMoveKind.ArcClockwise, x, y, null, i, j, feed);
        }

        /// <summary>Дуга против часовой стрелки в точку (x, y) вокруг центра (i, j).</summary>
        public void ArcCCW(double x, double y, double i, double j, double feed)
        {
            Add(ToolMoveKind.ArcCounterClockwise, x, y, null, i, j, feed);
        }

        private void Add(
            ToolMoveKind kind, double? x, double? y, double? z, double? i, double? j, double? feed)
        {
            _operation.Items.Add(new ToolMove(kind, x, y, z, i, j, feed));
        }
    }
}
