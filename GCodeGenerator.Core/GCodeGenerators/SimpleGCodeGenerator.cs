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

            builder.Header();

            // Установка рабочей системы координат (G54-G59) в самом начале программы
            if (settings.SetWorkCoordinateSystem && !string.IsNullOrEmpty(settings.WorkCoordinateSystem))
            {
                var wcs = settings.WorkCoordinateSystem.Trim().ToUpperInvariant();
                // Проверяем, что это валидная команда G54-G59
                if (wcs is "G54" or "G55" or "G56" or "G57" or "G58" or "G59")
                {
                    builder.SetWcs(wcs);
                }
            }

            // Установка стартовых координат (G92) сразу после комментариев
            if (settings.AddStartPosition)
            {
                builder.SetStartPosition(settings.StartX, settings.StartY, settings.StartZ);
            }

            if (settings.SpindleControlEnabled)
            {
                if (settings.SpindleStartEnabled)
                {
                    var cmd = (settings.SpindleStartCommand ?? "M3").Trim().ToUpperInvariant();
                    if (cmd != "M3" && cmd != "M4")
                        cmd = "M3";
                    builder.SpindleOn(cmd, settings.SpindleSpeedEnabled ? (int?)settings.SpindleSpeedRpm : null);
                }

                if (settings.CoolantControlEnabled && settings.CoolantStartEnabled)
                    builder.CoolantOn();

                if (settings.SpindleDelayEnabled && settings.SpindleDelaySeconds > 0)
                    builder.Dwell(settings.SpindleDelaySeconds * 1000.0);
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

            if (settings.CoolantControlEnabled && settings.CoolantStopEnabled)
                builder.CoolantOff();

            if (settings.AddEndPosition)
            {
                builder.SetEndPosition(settings.EndX, settings.EndY, settings.EndZ);
            }

            if (settings.SpindleControlEnabled && settings.SpindleStopEnabled)
                builder.SpindleOff();

            builder.EndProgram();

            GCodeFormatter.Format(program, settings);
            return program;
        }
    }
}
