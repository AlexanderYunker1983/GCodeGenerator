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
        private static readonly JsonSerializerOptions SettingsOptions = new JsonSerializerOptions();

        private GenerationSnapshot(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
        {
            Operations = operations;
            Settings = settings;
        }

        /// <summary>Копии операций в порядке обработки.</summary>
        public IReadOnlyList<OperationBase> Operations { get; }

        /// <summary>Копия настроек генерации.</summary>
        public GCodeSettings Settings { get; }

        /// <summary>
        /// Снимает слепок: каждая операция и настройки копируются, поэтому
        /// дальнейшие изменения документа на результат не влияют.
        /// </summary>
        /// <param name="operations">Операции документа.</param>
        /// <param name="settings">Настройки генерации.</param>
        public static GenerationSnapshot Capture(IEnumerable<OperationBase> operations, GCodeSettings settings)
        {
            if (operations == null)
                throw new ArgumentNullException(nameof(operations));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var copies = new List<OperationBase>();
            foreach (var operation in operations)
                copies.Add(operation == null ? null : OperationCloner.Clone(operation));

            return new GenerationSnapshot(copies, CloneSettings(settings));
        }

        /// <summary>
        /// Копия настроек. Группы настроек — обычные классы со свойствами,
        /// поэтому копируется весь объект целиком: новая группа или новый
        /// параметр попадут в слепок без правки этого метода.
        /// </summary>
        private static GCodeSettings CloneSettings(GCodeSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, SettingsOptions);
            return JsonSerializer.Deserialize<GCodeSettings>(json, SettingsOptions)
                ?? throw new InvalidOperationException("Не удалось создать копию настроек генерации.");
        }
    }
}
