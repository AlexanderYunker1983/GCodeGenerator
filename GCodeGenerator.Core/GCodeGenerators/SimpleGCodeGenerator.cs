using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class SimpleGCodeGenerator : IGCodeGenerator
    {
        private readonly IOperationGeneratorRegistry _registry;

        /// <summary>
        /// Пункт 4.5 плана: генераторы берутся из явного реестра
        /// (<see cref="OperationGeneratorRegistry"/>), name-based рефлексия удалена.
        /// </summary>
        public SimpleGCodeGenerator() : this(new OperationGeneratorRegistry())
        {
        }

        public SimpleGCodeGenerator(IOperationGeneratorRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings)
        {
            // План 4.3/4.4: программа собирается структурой (ProgramBuilder)
            // и рендерится GCodeFormatter; операционные генераторы пишут
            // блоки через ProgramBuilder (IOperationGenerator).
            var program = new GCodeProgram();
            var builder = new ProgramBuilder(program);

            // Пункт 8.1 плана: настройки — через тематические группы.
            var spindle = settings.Spindle;
            var coolant = settings.Coolant;
            var workCoordinate = settings.WorkCoordinate;

            builder.Header();

            // Установка рабочей системы координат (G54-G59) в самом начале программы
            if (workCoordinate.SetWorkCoordinateSystem && !string.IsNullOrEmpty(workCoordinate.WorkCoordinateSystem))
            {
                var wcs = workCoordinate.WorkCoordinateSystem.Trim().ToUpperInvariant();
                // Проверяем, что это валидная команда G54-G59
                if (wcs is "G54" or "G55" or "G56" or "G57" or "G58" or "G59")
                {
                    builder.SetWcs(wcs);
                }
            }

            // Установка стартовых координат (G92) сразу после комментариев
            if (workCoordinate.AddStartPosition)
            {
                builder.SetStartPosition(workCoordinate.StartX, workCoordinate.StartY, workCoordinate.StartZ);
            }

            if (spindle.SpindleControlEnabled)
            {
                if (spindle.SpindleStartEnabled)
                {
                    var cmd = (spindle.SpindleStartCommand ?? "M3").Trim().ToUpperInvariant();
                    if (cmd != "M3" && cmd != "M4")
                        cmd = "M3";
                    builder.SpindleOn(cmd, spindle.SpindleSpeedEnabled ? (int?)spindle.SpindleSpeedRpm : null);
                }

                if (coolant.CoolantControlEnabled && coolant.CoolantStartEnabled)
                    builder.CoolantOn();

                if (spindle.SpindleDelayEnabled && spindle.SpindleDelaySeconds > 0)
                    builder.Dwell(spindle.SpindleDelaySeconds * 1000.0);
            }

            foreach (var operation in operations)
            {
                // Skip disabled operations completely when generating trajectory
                if (operation == null || !operation.IsEnabled)
                    continue;

                builder.Comment($"{operation.Name}: {operation.GetDescription()}");

                var operationType = operation.GetType();
                if (_registry.TryGetGenerator(operationType, out var generator))
                {
                    generator.Generate(operation, builder, settings);
                }
            }

            if (coolant.CoolantControlEnabled && coolant.CoolantStopEnabled)
                builder.CoolantOff();

            if (workCoordinate.AddEndPosition)
            {
                builder.SetEndPosition(workCoordinate.EndX, workCoordinate.EndY, workCoordinate.EndZ);
            }

            if (spindle.SpindleControlEnabled && spindle.SpindleStopEnabled)
                builder.SpindleOff();

            builder.EndProgram();

            GCodeFormatter.Format(program, settings);
            return program;
        }
    }
}
