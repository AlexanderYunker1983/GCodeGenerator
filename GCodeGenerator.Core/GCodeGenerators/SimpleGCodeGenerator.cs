using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;
using GCodeGenerator.Toolpath;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Собирает траекторию инструмента по операциям проекта и отдаёт её
    /// постпроцессору, который превращает движение в программу для станка.
    ///
    /// Раньше генератор делал и то и другое сразу: обход операций шёл
    /// вперемешку с выводом G-слов, командами шпинделя и охлаждения, поэтому
    /// диалект стойки был размазан по генератору, построителю программы
    /// и всем стратегиям выборки. Теперь генератор знает только о движении
    /// инструмента, а всё, что зависит от станка, живёт в постпроцессоре.
    /// </summary>
    public class SimpleGCodeGenerator : IGCodeGenerator
    {
        private readonly IOperationGeneratorRegistry _registry;
        private readonly IPostProcessor _postProcessor;

        /// <summary>
        /// Пункт 4.5 плана: генераторы берутся из явного реестра
        /// (<see cref="OperationGeneratorRegistry"/>), name-based рефлексия удалена.
        /// </summary>
        public SimpleGCodeGenerator() : this(new OperationGeneratorRegistry())
        {
        }

        public SimpleGCodeGenerator(IOperationGeneratorRegistry registry)
            : this(registry, new GenericPostProcessor())
        {
        }

        public SimpleGCodeGenerator(IOperationGeneratorRegistry registry, IPostProcessor postProcessor)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _postProcessor = postProcessor ?? throw new ArgumentNullException(nameof(postProcessor));
        }

        /// <inheritdoc />
        public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
        {
            var toolPath = BuildToolPath(operations, settings, progress);
            return _postProcessor.Build(toolPath, settings);
        }

        /// <inheritdoc />
        public ToolPath BuildToolPath(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Все проверки выполняются до построения траектории: при любой
            // ошибке вызывающая сторона не получит частичный, внешне
            // корректный результат.
            var resolvedGenerators = ValidateAndResolveGenerators(operations, settings);

            var toolPath = new ToolPath();

            // Пункт 8.4 плана: прогресс по операциям (0–100) — для async-генерации в UI.
            var total = operations.Count;
            for (var index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];

                // Skip disabled operations completely when generating trajectory
                if (operation == null || !operation.IsEnabled)
                    continue;

                var pathOperation = new ToolPathOperation(
                    operation.Name, operation.GetDescription(), OperationDecimals(operation));
                toolPath.Operations.Add(pathOperation);

                resolvedGenerators[index].Generate(operation, new ToolPathBuilder(pathOperation), settings);

                if (total > 0)
                    progress?.Report((index + 1) * 100 / total);
            }

            return toolPath;
        }

        /// <summary>
        /// Точность вывода координат операции. У сверления и фрезерования она
        /// объявлена по-разному, но означает одно и то же.
        /// </summary>
        private static int OperationDecimals(OperationBase operation)
        {
            switch (operation)
            {
                case MillingOperationBase milling:
                    return milling.Decimals;
                case DrillPointsOperation drill:
                    return drill.Decimals;
                default:
                    return 3;
            }
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
