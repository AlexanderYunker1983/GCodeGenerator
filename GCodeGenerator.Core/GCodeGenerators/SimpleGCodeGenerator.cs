using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GCodeGenerator.GCodeGenerators.Interfaces;
using GCodeGenerator.Models;

namespace GCodeGenerator.GCodeGenerators
{
    public class SimpleGCodeGenerator : IGCodeGenerator
    {
        private readonly Dictionary<Type, IOperationGenerator> _generators = new Dictionary<Type, IOperationGenerator>();

        public SimpleGCodeGenerator()
        {
            LoadGenerators();
        }

        private void LoadGenerators()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var generatorTypes = assembly.GetTypes()
                .Where(t => typeof(IOperationGenerator).IsAssignableFrom(t) 
                    && !t.IsInterface 
                    && !t.IsAbstract);

            // Сначала регистрируем единые генераторы для профилей и карманов
            var unifiedProfileGenerator = generatorTypes.FirstOrDefault(t => t.Name == "UnifiedProfileGenerator");
            var unifiedPocketGenerator = generatorTypes.FirstOrDefault(t => t.Name == "UnifiedPocketGenerator");

            if (unifiedProfileGenerator != null)
            {
                var generator = (IOperationGenerator)Activator.CreateInstance(unifiedProfileGenerator);
                // Регистрируем для всех типов профилей
                var profileOperationTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => typeof(OperationBase).IsAssignableFrom(t) 
                        && typeof(IProfileOperation).IsAssignableFrom(t)
                        && !t.IsAbstract);
                
                foreach (var operationType in profileOperationTypes)
                {
                    _generators[operationType] = generator;
                }
            }

            if (unifiedPocketGenerator != null)
            {
                var generator = (IOperationGenerator)Activator.CreateInstance(unifiedPocketGenerator);
                // Регистрируем для всех типов карманов
                var pocketOperationTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .Where(t => typeof(OperationBase).IsAssignableFrom(t) 
                        && typeof(IPocketOperation).IsAssignableFrom(t)
                        && !t.IsAbstract);
                
                foreach (var operationType in pocketOperationTypes)
                {
                    _generators[operationType] = generator;
                }
            }

            // Затем регистрируем остальные генераторы (игнорируя единые генераторы профилей и карманов)
            var excludedGenerators = new[] 
            { 
                "UnifiedProfileGenerator", 
                "UnifiedPocketGenerator"
            };

            foreach (var generatorType in generatorTypes)
            {
                if (excludedGenerators.Contains(generatorType.Name))
                    continue;

                var generator = (IOperationGenerator)Activator.CreateInstance(generatorType);
                var operationTypeName = generatorType.Name.Replace("Generator", "");
                
                var operationType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == operationTypeName && typeof(OperationBase).IsAssignableFrom(t));
                
                if (operationType != null && !_generators.ContainsKey(operationType))
                {
                    _generators[operationType] = generator;
                }
            }
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
                if (_generators.TryGetValue(operationType, out var generator))
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
