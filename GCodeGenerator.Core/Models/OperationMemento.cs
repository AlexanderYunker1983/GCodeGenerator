#nullable enable
using System;
using System.Text.Json;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Слепок операции: тип, текст и идентификатор.
    ///
    /// Нужен там, где состояние операции переживает саму операцию: шаг
    /// истории изменений хранит «как было» и «как стало» и восстанавливает
    /// любое из них по требованию. Сериализатор тот же, что у файла проекта
    /// и <see cref="OperationCloner"/>: состав восстанавливаемых данных по
    /// определению совпадает с составом сохраняемых. Идентификатор в текст
    /// не пишется и переносится явно — по нему восстановленную копию
    /// признают той же операцией документа.
    /// </summary>
    public sealed class OperationMemento
    {
        // Общие настройки сериализации с файлом проекта (см. ProjectJson).
        private static readonly JsonSerializerOptions Options = ProjectJson.Options;

        private readonly Type _type;

        private OperationMemento(Type type, string json, Guid id)
        {
            _type = type;
            Json = json;
            Id = id;
        }

        /// <summary>Идентификатор операции документа.</summary>
        public Guid Id { get; }

        /// <summary>
        /// Текстовая форма состояния; по её равенству два слепка одной
        /// операции сравниваются на «изменилось ли что-нибудь».
        /// </summary>
        public string Json { get; }

        /// <summary>Снимает слепок текущего состояния операции.</summary>
        /// <param name="operation">Операция документа.</param>
        public static OperationMemento Of(OperationBase operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            return new OperationMemento(
                operation.GetType(),
                JsonSerializer.Serialize(operation, operation.GetType(), Options),
                operation.Id);
        }

        /// <summary>Восстанавливает копию операции в состоянии слепка.</summary>
        public OperationBase Restore()
        {
            if (!(JsonSerializer.Deserialize(Json, _type, Options) is OperationBase operation))
                throw new InvalidOperationException($"Failed to restore the operation {_type.Name}.");

            operation.Id = Id;
            return operation;
        }
    }
}
