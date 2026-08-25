using System;
using System.Collections.Generic;
using System.Linq;
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

        public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Все проверки выполняются до создания блоков программы: при любой
            // ошибке вызывающая сторона не получит частичный, внешне корректный G-code.
            var resolvedGenerators = ValidateAndResolveGenerators(operations, settings);

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

            // Модальные состояния станка задаются до первого перемещения:
            // иначе программа зависит от того, что выполнялось на стойке до неё.
            builder.SafetyPreamble();

            // Установка рабочей системы координат (G54-G59) в самом начале программы.
            // Значение уже проверено предполётным разбором: неверное отклоняется
            // с ошибкой, а не пропускается молча.
            if (workCoordinate.SetWorkCoordinateSystem)
                builder.SetWcs(workCoordinate.WorkCoordinateSystem.Trim().ToUpperInvariant());

            // Установка стартовых координат (G92) сразу после комментариев
            if (workCoordinate.AddStartPosition)
            {
                builder.SetStartPosition(workCoordinate.StartX, workCoordinate.StartY, workCoordinate.StartZ);
            }

            if (spindle.SpindleControlEnabled)
            {
                if (spindle.SpindleStartEnabled)
                {
                    // Команда проверена предполётным разбором: направление
                    // вращения не подменяется тихо на «по часовой».
                    var cmd = spindle.SpindleStartCommand.Trim().ToUpperInvariant();
                    builder.SpindleOn(cmd, spindle.SpindleSpeedEnabled ? (int?)spindle.SpindleSpeedRpm : null);
                }

                if (coolant.CoolantControlEnabled && coolant.CoolantStartEnabled)
                    builder.CoolantOn();

                if (spindle.SpindleDelayEnabled && spindle.SpindleDelaySeconds > 0)
                    builder.Dwell(spindle.SpindleDelaySeconds * 1000.0);
            }

            // Пункт 8.4 плана: прогресс по операциям (0–100) — для async-генерации в UI.
            var total = operations.Count;
            for (var index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];

                // Skip disabled operations completely when generating trajectory
                if (operation == null || !operation.IsEnabled)
                    continue;

                builder.Comment($"{operation.Name}: {operation.GetDescription()}");

                resolvedGenerators[index].Generate(operation, builder, settings);

                if (total > 0)
                    progress?.Report((index + 1) * 100 / total);
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

        private IOperationGenerator[] ValidateAndResolveGenerators(IList<OperationBase> operations, GCodeSettings settings)
        {
            var failures = new List<OperationValidationFailure>();
            var generators = new IOperationGenerator[operations.Count];

            // Настройки проверяются вместе с операциями, чтобы пользователь
            // увидел все причины отказа сразу.
            var settingsIssues = GCodeSettingsValidation.Validate(settings);

            for (int index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                if (operation == null)
                {
                    failures.Add(new OperationValidationFailure(
                        index,
                        null,
                        null,
                        new[] { new ValidationIssue("Operation", "operation is null") }));
                    continue;
                }

                if (!operation.IsEnabled)
                    continue;

                var issues = new List<ValidationIssue>();
                if (!_registry.TryGetGenerator(operation.GetType(), out var generator) || generator == null)
                {
                    issues.Add(new ValidationIssue(
                        "OperationType",
                        "no G-code generator is registered for this operation type"));
                }
                else
                {
                    generators[index] = generator;
                }

                if (operation is IValidatable validatable)
                {
                    var validationIssues = validatable.Validate();
                    if (validationIssues == null)
                    {
                        issues.Add(new ValidationIssue("Validation", "validator returned null"));
                    }
                    else
                    {
                        issues.AddRange(validationIssues.Where(issue => issue != null));
                    }
                }
                else
                {
                    issues.Add(new ValidationIssue(
                        "Validation",
                        "operation type does not implement domain validation"));
                }

                if (issues.Count > 0)
                {
                    failures.Add(new OperationValidationFailure(
                        index,
                        operation.Name,
                        operation.GetType().Name,
                        issues));
                }
            }

            if (failures.Count > 0 || settingsIssues.Count > 0)
                throw new GCodeGenerationValidationException(failures, settingsIssues);

            return generators;
        }
    }
}
