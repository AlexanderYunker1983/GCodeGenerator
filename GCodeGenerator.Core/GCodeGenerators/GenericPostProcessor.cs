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
    /// Стойки с другой единицей аргумента паузы описываются наследником
    /// (<see cref="GrblPostProcessor"/>), а не ветвлением в генераторах.
    /// </summary>
    public class GenericPostProcessor : IPostProcessor
    {
        /// <summary>Секунда в миллисекундах: пауза задаётся в секундах, выводится в них.</summary>
        private const double MillisecondsPerSecond = 1000.0;

        /// <inheritdoc />
        public virtual string Key => "Generic";

        /// <inheritdoc />
        public virtual string Name => "Generic (Fanuc-compatible)";

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
            WriteFooter(builder, toolPath, settings);

            GCodeFormatter.Format(program, settings);
            return program;
        }

        /// <summary>
        /// Аргумент P команды паузы G4 для заданной длительности. Fanuc
        /// и совместимые стойки понимают миллисекунды; GRBL и LinuxCNC —
        /// секунды, и их постпроцессор переопределяет ровно это
        /// преобразование, не трогая состав программы.
        /// </summary>
        /// <param name="seconds">Длительность паузы в секундах, как она задана в настройках.</param>
        protected virtual double DwellArgument(double seconds) => seconds * MillisecondsPerSecond;

        /// <summary>
        /// Начало программы: подпись, модальные состояния станка, ноль детали
        /// и запуск шпинделя с охлаждением.
        /// </summary>
        private void WriteHeader(ProgramBuilder builder, GCodeSettings settings)
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
                builder.Dwell(DwellArgument(spindle.SpindleDelaySeconds));
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
            // Дуга — отдельный тип с гарантированными величинами: кадр G2/G3
            // без конечной точки, центра или подачи непредставим уже при
            // создании перемещения, и выводу перепроверять нечего.
            if (move is ArcMove arc)
            {
                WriteArc(builder, arc, decimals);
                return;
            }

            switch (move.Kind)
            {
                case ToolMoveKind.Rapid:
                    builder.RapidTo(move.X, move.Y, move.Z, move.Feed, decimals);
                    break;
                case ToolMoveKind.Linear:
                    builder.LinearTo(move.X, move.Y, move.Z, move.Feed, decimals);
                    break;
            }
        }

        /// <summary>
        /// Дуга описывается конечной точкой, смещением центра и подачей —
        /// все пять величин обязательны, иначе кадр G2/G3 не имеет смысла.
        /// Обязательность обеспечивает конструктор <see cref="ArcMove"/>:
        /// прежде постпроцессор перепроверял величины при выводе и называл
        /// пропавшую по имени, теперь тот же именованный отказ приходит
        /// раньше — в месте, где дугу собрали.
        /// </summary>
        /// <param name="builder">Построитель программы.</param>
        /// <param name="arc">Перемещение по дуге.</param>
        /// <param name="decimals">Число знаков после запятой в координатах.</param>
        private static void WriteArc(ProgramBuilder builder, ArcMove arc, int decimals)
        {
            if (arc.Kind == ToolMoveKind.ArcClockwise)
                builder.ArcCW(
                    arc.EndX, arc.EndY, arc.ArcCenterOffsetX, arc.ArcCenterOffsetY,
                    arc.ArcFeed, decimals, arc.EndZ);
            else
                builder.ArcCCW(
                    arc.EndX, arc.EndY, arc.ArcCenterOffsetX, arc.ArcCenterOffsetY,
                    arc.ArcFeed, decimals, arc.EndZ);
        }

        /// <summary>
        /// Конец программы: вертикальный отвод из материала, остановка
        /// шпинделя и охлаждения, горизонтальная парковка и завершение.
        /// </summary>
        private static void WriteFooter(ProgramBuilder builder, ToolPath toolPath, GCodeSettings settings)
        {
            var spindle = settings.Spindle;
            var coolant = settings.Coolant;
            var workCoordinate = settings.WorkCoordinate;

            if (workCoordinate.AddEndPosition)
            {
                // Сначала только Z: одновременный G0 X/Y/Z строит диагональ,
                // которая может пересечь заготовку или оснастку. Высота
                // берётся из выполняемых операций, а не угадывается по EndZ.
                builder.RapidTo(z: EndClearanceZ(toolPath, workCoordinate.EndZ));
            }

            if (spindle.SpindleControlEnabled && spindle.SpindleStopEnabled)
                builder.SpindleOff();

            if (coolant.CoolantControlEnabled && coolant.CoolantStopEnabled)
                builder.CoolantOff();

            if (workCoordinate.AddEndPosition)
            {
                var clearanceZ = EndClearanceZ(toolPath, workCoordinate.EndZ);
                builder.RapidTo(x: workCoordinate.EndX, y: workCoordinate.EndY);
                if (workCoordinate.EndZ != clearanceZ)
                    builder.RapidTo(z: workCoordinate.EndZ);
            }

            builder.EndProgram();
        }

        /// <summary>Наибольшая безопасная высота всех выполненных операций.</summary>
        private static double EndClearanceZ(ToolPath toolPath, double endZ)
        {
            var clearanceZ = endZ;
            foreach (var operation in toolPath.Operations)
            {
                switch (operation.Source)
                {
                    case MillingOperationBase milling:
                        clearanceZ = Math.Max(clearanceZ, milling.SafeZHeight);
                        break;
                    case DrillPointsOperation drill:
                        clearanceZ = Math.Max(clearanceZ, drill.SafeZBetweenHoles);
                        break;
                }
            }

            return clearanceZ;
        }
    }
}
