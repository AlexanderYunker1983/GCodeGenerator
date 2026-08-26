#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Неизменный слепок документа для генерации G-code: операции и настройки
    /// на момент нажатия кнопки.
    ///
    /// Генерация выполняется в фоновом потоке и при этом не блокирует окно:
    /// пока она идёт, пользователь может отредактировать операцию, снять
    /// галочку «включена» или изменить настройки. Раньше в фон уходил
    /// поверхностный список — сами операции и объект настроек оставались
    /// общими с интерфейсом, поэтому программа могла собраться наполовину
    /// из старого, наполовину из нового состояния. Признак изменения документа
    /// отбрасывает такой результат, но лишь после того, как он уже собран
    /// из смешанных данных.
    ///
    /// Копии делаются тем же сериализатором, что и файл проекта
    /// (<see cref="OperationCloner"/>): состав слепка по определению совпадает
    /// с составом сохраняемых данных, а вложенные списки — отверстия,
    /// полилинии, контуры — копируются целиком.
    /// </summary>
    public sealed class GenerationSnapshot
    {
        // Общие настройки сериализации с файлом проекта (см. ProjectJson).
        private static readonly JsonSerializerOptions SettingsOptions = ProjectJson.Options;

        private GenerationSnapshot(IReadOnlyList<OperationBase?> operations, GCodeSettings settings)
        {
            Operations = operations;
            Settings = settings;
        }

        /// <summary>Копии операций в порядке обработки.</summary>
        public IReadOnlyList<OperationBase?> Operations { get; }

        /// <summary>Копия настроек генерации.</summary>
        public GCodeSettings Settings { get; }

        /// <summary>
        /// Снимает слепок: каждая операция и настройки копируются, поэтому
        /// дальнейшие изменения документа на результат не влияют.
        /// </summary>
        /// <param name="operations">Операции документа.</param>
        /// <param name="settings">Настройки генерации.</param>
        public static GenerationSnapshot Capture(IEnumerable<OperationBase?> operations, GCodeSettings settings)
            => Serialize(operations, settings).Deserialize();

        /// <summary>
        /// Первая стадия слепка: операции и настройки превращаются в текст.
        /// Выполняется на потоке интерфейса — документ нельзя читать из фона,
        /// пока его может править пользователь, — и это дешёвая половина
        /// работы; дорогая материализация копий уходит в фон
        /// (<see cref="Serialized.Deserialize"/>).
        /// </summary>
        /// <param name="operations">Операции документа.</param>
        /// <param name="settings">Настройки генерации.</param>
        public static Serialized Serialize(IEnumerable<OperationBase?> operations, GCodeSettings settings)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Пустая операция в списке возможна: файл проекта, написанный
            // вручную, способен принести и такое — снимок сохраняет её как
            // есть, а отклонит её проверка перед генерацией.
            var entries = new List<Serialized.Entry?>();
            foreach (var operation in operations)
            {
                entries.Add(operation == null
                    ? null
                    : new Serialized.Entry(
                        operation.GetType(),
                        JsonSerializer.Serialize(operation, operation.GetType(), SettingsOptions),
                        operation.Id));
            }

            return new Serialized(entries, JsonSerializer.Serialize(settings, SettingsOptions));
        }

        /// <summary>
        /// Сериализованный слепок — текстовая форма документа между потоками.
        /// Состав копий тот же, что у <see cref="OperationCloner"/> и файла
        /// проекта (общие настройки сериализации); идентификатор операции
        /// в файл не пишется и переносится явно — по нему предпросмотр
        /// сопоставляет копию с операцией документа.
        /// </summary>
        public sealed class Serialized
        {
            private readonly IReadOnlyList<Entry?> _operations;
            private readonly string _settings;

            internal Serialized(IReadOnlyList<Entry?> operations, string settings)
            {
                _operations = operations;
                _settings = settings;
            }

            /// <summary>
            /// Вторая стадия слепка: материализация копий. Выполняется в фоне —
            /// текст уже ни с кем не разделён, и интерфейс это не задерживает.
            /// </summary>
            public GenerationSnapshot Deserialize()
            {
                var copies = new List<OperationBase?>();
                foreach (var entry in _operations)
                {
                    if (entry == null)
                    {
                        copies.Add(null);
                        continue;
                    }

                    if (!(JsonSerializer.Deserialize(entry.Json, entry.Type, SettingsOptions) is OperationBase copy))
                        throw new InvalidOperationException($"Failed to clone the operation {entry.Type.Name}.");

                    copy.Id = entry.Id;
                    copies.Add(copy);
                }

                var settings = JsonSerializer.Deserialize<GCodeSettings>(_settings, SettingsOptions)
                    ?? throw new InvalidOperationException("Failed to clone the generation settings.");

                return new GenerationSnapshot(copies, settings);
            }

            /// <summary>Одна операция в текстовой форме.</summary>
            internal sealed class Entry
            {
                public Entry(Type type, string json, Guid id)
                {
                    Type = type;
                    Json = json;
                    Id = id;
                }

                public Type Type { get; }

                public string Json { get; }

                public Guid Id { get; }
            }
        }
    }
}
