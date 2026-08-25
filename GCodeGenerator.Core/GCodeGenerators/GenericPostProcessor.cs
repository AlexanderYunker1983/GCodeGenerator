#nullable enable
using System;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Постпроцессор общего назначения: миллиметры, абсолютные координаты,
    /// подача в минуту, дуги через смещения центра, пауза в миллисекундах.
    ///
    /// Такой вывод понимают стойки Fanuc и совместимые с ними. Он же
    /// действовал в программе всегда — просто нигде не был назван, а его
    /// правила лежали в генераторе вперемешку с обходом операций.
    /// Единица аргумента паузы для GRBL и LinuxCNC другая (секунды), и когда
    /// понадобится их поддержать, отличаться будет этот класс, а не генераторы.
    /// </summary>
    public sealed class GenericPostProcessor : IPostProcessor
    {
        /// <summary>Секунда в миллисекундах: пауза задаётся в секундах, выводится в них.</summary>
        private const double MillisecondsPerSecond = 1000.0;

        /// <inheritdoc />
        public string Name => "Generic (Fanuc-compatible)";

        /// <inheritdoc />
        public GCodeProgram Build(ToolPath toolPath, GCodeSettings settings)
        {
            if (toolPath == null)
                throw new ArgumentNullException(nameof(toolPath));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var program = new GCodeProgram();
            var builder = new ProgramBuilder(program);

            WriteHeader(builder, settings);
            WriteOperations(builder, toolPath);
            WriteFooter(builder, settings);

            GCodeFormatter.Format(program, settings);
            return program;
        }

        /// <summary>
        /// Начало программы: подпись, модальные состояния станка, ноль детали
        /// и запуск шпинделя с охлаждением.
        /// </summary>
        private static void WriteHeader(ProgramBuilder builder, GCodeSettings settings)
        {
            var spindle = settings.Spindle;
            var coolant = settings.Coolant;
            var workCoordinate = settings.WorkCoordinate;

            builder.Header();

            // Модальные состояния задаются до первого перемещения: иначе
            // программа зависит от того, что выполнялось на стойке до неё.
            builder.SafetyPreamble();

            if (workCoordinate.SetWorkCoordinateSystem)
                builder.SetWcs(workCoordinate.WorkCoordinateSystem.Trim().ToUpperInvariant());

            if (workCoordinate.AddStartPosition)
                builder.SetStartPosition(workCoordinate.StartX, workCoordinate.StartY, workCoordinate.StartZ);

            if (!spindle.SpindleControlEnabled)
                return;

            if (spindle.SpindleStartEnabled)
            {
                builder.SpindleOn(
                    spindle.SpindleStartCommand.Trim().ToUpperInvariant(),
                    spindle.SpindleSpeedEnabled ? (int?)spindle.SpindleSpeedRpm : null);
            }

            if (coolant.CoolantControlEnabled && coolant.CoolantStartEnabled)
                builder.CoolantOn();

            if (spindle.SpindleDelayEnabled && spindle.SpindleDelaySeconds > 0)
                builder.Dwell(spindle.SpindleDelaySeconds * MillisecondsPerSecond);
        }

        /// <summary>Траектория операций: комментарий с именем и сами перемещения.</summary>
        private static void WriteOperations(ProgramBuilder builder, ToolPath toolPath)
        {
            foreach (var operation in toolPath.Operations)
            {
                builder.Comment(ProgramComments.Operation(operation.Name, operation.Description));

                foreach (var item in operation.Items)
                {
                    switch (item)
                    {
                        case ToolPathNote note:
                            builder.Comment(note.Text);
                            break;
                        case ToolMove move:
                            WriteMove(builder, move, operation.Decimals);
                            break;
                    }
                }
            }
        }

        private static void WriteMove(ProgramBuilder builder, ToolMove move, int decimals)
        {
            switch (move.Kind)
            {
                case ToolMoveKind.Rapid:
                    builder.RapidTo(move.X, move.Y, move.Z, move.Feed, decimals);
                    break;
                case ToolMoveKind.Linear:
                    builder.LinearTo(move.X, move.Y, move.Z, move.Feed, decimals);
                    break;
                case ToolMoveKind.ArcClockwise:
                case ToolMoveKind.ArcCounterClockwise:
                    WriteArc(builder, move, decimals);
                    break;
            }
        }

        /// <summary>
        /// Дуга описывается конечной точкой, смещением центра и подачей —
        /// все пять величин обязательны, иначе кадр G2/G3 не имеет смысла.
        /// Построитель траектории задаёт их всегда; проверка стоит здесь,
        /// потому что траектория может прийти и из файла, и из чужого кода,
        /// а без неё отсутствующая величина превратилась бы в исключение
        /// без единого указания на то, какая именно и в какой операции.
        /// </summary>
        /// <param name="builder">Построитель программы.</param>
        /// <param name="move">Перемещение по дуге.</param>
        /// <param name="decimals">Число знаков после запятой в координатах.</param>
        private static void WriteArc(ProgramBuilder builder, ToolMove move, int decimals)
        {
            var x = Required(move.X, nameof(move.X), move.Kind);
            var y = Required(move.Y, nameof(move.Y), move.Kind);
            var offsetX = Required(move.CenterOffsetX, nameof(move.CenterOffsetX), move.Kind);
            var offsetY = Required(move.CenterOffsetY, nameof(move.CenterOffsetY), move.Kind);
            var feed = Required(move.Feed, nameof(move.Feed), move.Kind);

            if (move.Kind == ToolMoveKind.ArcClockwise)
                builder.ArcCW(x, y, offsetX, offsetY, feed, decimals);
            else
                builder.ArcCCW(x, y, offsetX, offsetY, feed, decimals);
        }

        /// <summary>Величина, без которой перемещение не описывает движение.</summary>
        /// <param name="value">Заданное значение или пустота.</param>
        /// <param name="name">Имя величины для сообщения об ошибке.</param>
        /// <param name="kind">Вид перемещения.</param>
        private static double Required(double? value, string name, ToolMoveKind kind)
            => value ?? throw new InvalidOperationException(
                $"У перемещения {kind} не задана величина {name}.");

        /// <summary>
        /// Конец программы: выключение охлаждения, отход в конечную точку,
        /// остановка шпинделя и завершение.
        /// </summary>
        private static void WriteFooter(ProgramBuilder builder, GCodeSettings settings)
        {
            var spindle = settings.Spindle;
            var coolant = settings.Coolant;
            var workCoordinate = settings.WorkCoordinate;

            if (coolant.CoolantControlEnabled && coolant.CoolantStopEnabled)
                builder.CoolantOff();

            if (workCoordinate.AddEndPosition)
                builder.SetEndPosition(workCoordinate.EndX, workCoordinate.EndY, workCoordinate.EndZ);

            if (spindle.SpindleControlEnabled && spindle.SpindleStopEnabled)
                builder.SpindleOff();

            builder.EndProgram();
        }
    }
}
