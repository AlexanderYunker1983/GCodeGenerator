// Проверка ссылок на пустоту включена для генераторов вслед за моделями:
// именно здесь пустота, пришедшая из модели или из файла проекта, дошла бы
// до построения траектории. Директива стоит пофайлово — включение на весь
// продукт разом дало бы около девятисот предупреждений; следующий шаг —
// приложение.
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
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
        private readonly IPostProcessorRegistry _postProcessors;

        /// <summary>
        /// Пункт 4.5 плана: генераторы берутся из явного реестра
        /// (<see cref="OperationGeneratorRegistry"/>), name-based рефлексия удалена.
        /// </summary>
        public SimpleGCodeGenerator() : this(new OperationGeneratorRegistry())
        {
        }

        public SimpleGCodeGenerator(IOperationGeneratorRegistry registry)
            : this(registry, new PostProcessorRegistry())
        {
        }

        /// <summary>
        /// Генератор с внешним реестром постпроцессоров: стойка выбирается
        /// настройкой <see cref="GCodeFormatSettings.PostProcessorName"/>
        /// при каждой генерации, а не фиксируется при создании генератора.
        /// </summary>
        public SimpleGCodeGenerator(IOperationGeneratorRegistry registry, IPostProcessorRegistry postProcessors)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _postProcessors = postProcessors ?? throw new ArgumentNullException(nameof(postProcessors));
        }

        /// <inheritdoc />
        public GCodeProgram Generate(
            IReadOnlyList<OperationBase?> operations,
            GCodeSettings settings,
            IProgress<int>? progress = null,
            CancellationToken cancellation = default)
        {
            // Проверка настроек внутри BuildToolPath уже отказала бы на
            // неизвестном ключе, поэтому здесь выбор всегда удаётся.
            var toolPath = BuildToolPath(operations, settings, progress, cancellation);
            return _postProcessors.For(settings.Format?.PostProcessorName).Build(toolPath, settings);
        }

        /// <inheritdoc />
        public ToolPath BuildToolPath(
            IReadOnlyList<OperationBase?> operations,
            GCodeSettings settings,
            IProgress<int>? progress = null,
            CancellationToken cancellation = default)
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
                // Отмена проверяется между операциями, а внутри операции —
                // между её слоями и отверстиями (токен уходит в генератор).
                cancellation.ThrowIfCancellationRequested();

                var operation = operations[index];

                // Skip disabled operations completely when generating trajectory
                if (operation == null || !operation.IsEnabled)
                    continue;

                var pathOperation = new ToolPathOperation(
                    operation.Name, operation.GetDescription(), OperationDecimals(operation), operation);
                toolPath.AddOperation(pathOperation);

                resolvedGenerators[index].Generate(operation, new ToolPathBuilder(pathOperation), settings, cancellation);

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

        private IOperationGenerator[] ValidateAndResolveGenerators(IReadOnlyList<OperationBase?> operations, GCodeSettings settings)
        {
            var failures = new List<OperationValidationFailure>();
            var generators = new IOperationGenerator[operations.Count];

            // Настройки проверяются вместе с операциями, чтобы пользователь
            // увидел все причины отказа сразу.
            var settingsIssues = new List<ValidationIssue>(GCodeSettingsValidation.Validate(settings));

            // Ключ постпроцессора проверяется здесь, а не в общей проверке
            // настроек: список допустимых стоек знает реестр, а слой моделей
            // от генераторов не зависит.
            var postProcessorName = settings.Format?.PostProcessorName;
            if (_postProcessors.Find(postProcessorName) == null)
            {
                var known = string.Join(", ", _postProcessors.All.Select(p => p.Key));
                settingsIssues.Add(new ValidationIssue(
                    nameof(GCodeFormatSettings.PostProcessorName),
                    $"must be one of {known}, but is "
                    + (string.IsNullOrEmpty(postProcessorName) ? "empty" : $"\"{postProcessorName}\"")));
            }

            for (int index = 0; index < operations.Count; index++)
            {
                var operation = operations[index];
                if (operation == null)
                {
                    // Имени и описания у пустой операции нет: в отчёте она
                    // называется только своим местом в списке.
                    failures.Add(new OperationValidationFailure(
                        index,
                        operationName: null,
                        operationType: null,
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
