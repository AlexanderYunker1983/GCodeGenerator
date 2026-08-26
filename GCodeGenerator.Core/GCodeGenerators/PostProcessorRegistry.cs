#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GCodeGenerator.GCodeGenerators
{
    /// <summary>
    /// Соответствие «ключ в настройках → постпроцессор».
    /// </summary>
    public interface IPostProcessorRegistry
    {
        /// <summary>Все стойки, для которых продукт умеет строить программу.</summary>
        IReadOnlyList<IPostProcessor> All { get; }

        /// <summary>Постпроцессор по ключу; null — такого ключа нет.</summary>
        IPostProcessor? Find(string? key);

        /// <summary>Постпроцессор по ключу; неизвестный ключ — отказ.</summary>
        IPostProcessor For(string? key);
    }

    /// <summary>
    /// Реестр постпроцессоров — по образцу <see cref="OperationGeneratorRegistry"/>:
    /// экземпляр с интерфейсом, чтобы стойку можно было добавить через
    /// контейнер, не меняя генератор.
    ///
    /// Ключ сравнивается без учёта регистра: он попадает сюда из настроек
    /// и файла проекта, которые пользователь может редактировать руками,
    /// и «grbl» вместо «GRBL» не должен превращать проект в негодный.
    ///
    /// Неизвестный ключ — отказ с перечислением допустимых, а не подстановка
    /// Generic: единицы аргумента паузы у стоек разные, и программа,
    /// молча построенная не для той стойки, исполнялась бы неверно.
    /// </summary>
    public sealed class PostProcessorRegistry : IPostProcessorRegistry
    {
        private readonly IPostProcessor[] _postProcessors;

        /// <summary>Стандартный набор стоек продукта.</summary>
        public PostProcessorRegistry()
            : this(new IPostProcessor[] { new GenericPostProcessor(), new GrblPostProcessor() })
        {
        }

        /// <summary>Реестр из явного набора — для контейнера, расширений и тестов.</summary>
        /// <param name="postProcessors">Постпроцессоры; набор не пуст, ключи не повторяются.</param>
        public PostProcessorRegistry(IReadOnlyList<IPostProcessor> postProcessors)
        {
            if (postProcessors == null)
                throw new ArgumentNullException(nameof(postProcessors));

            // Реестр без единой стойки не может построить ничего: он отвергал
            // бы любые настройки с пустым перечнем допустимых. Такой набор —
            // ошибка конфигурации (контейнер без регистраций стоек), и о ней
            // нужно узнать при сборке приложения, а не отказом каждой генерации.
            if (postProcessors.Count == 0)
                throw new ArgumentException("At least one post-processor is required.", nameof(postProcessors));

            _postProcessors = postProcessors.ToArray();

            // Два постпроцессора с одним ключом означают, что выбор в
            // настройках неоднозначен: какой из них строил бы программу —
            // зависело бы от порядка регистрации.
            var duplicate = _postProcessors
                .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException(
                    FormattableString.Invariant($"Post-processor key \"{duplicate.Key}\" is registered more than once."),
                    nameof(postProcessors));
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IPostProcessor> All => _postProcessors;

        /// <inheritdoc />
        public IPostProcessor? Find(string? key)
        {
            foreach (var postProcessor in _postProcessors)
            {
                if (string.Equals(postProcessor.Key, key, StringComparison.OrdinalIgnoreCase))
                    return postProcessor;
            }

            return null;
        }

        /// <inheritdoc />
        public IPostProcessor For(string? key)
        {
            var found = Find(key);
            if (found != null)
                return found;

            var known = string.Join(", ", _postProcessors.Select(p => p.Key));
            throw new NotSupportedException(
                FormattableString.Invariant($"Post-processor \"{key}\" is not registered; known: {known}."));
        }
    }
}
