#nullable enable
using System;
using System.Text.Json;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Создаёт независимую копию операции.
    ///
    /// Копия нужна там, где обработка меняет параметры операции, не трогая
    /// исходную: черновой проход с припуском, чистовая обработка стенок и дна,
    /// модальный редактор операции. Раньше каждое такое место копировало поля
    /// вручную, перечисляя их по одному для каждого типа операции, — забытое
    /// при добавлении поле обнаруживалось только по неверному G-коду.
    ///
    /// Копирование выполняется через тот же сериализатор, что и файл проекта:
    /// состав копируемых данных по определению совпадает с составом
    /// сохраняемых, а вложенные списки (отверстия, полилинии, контуры)
    /// копируются целиком. Свойства, помеченные
    /// <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>
    /// (<see cref="OperationBase.Category"/>), восстанавливает конструктор
    /// конкретного типа операции.
    /// </summary>
    public static class OperationCloner
    {
        // Общие настройки сериализации с файлом проекта (см. ProjectJson):
        // «тот же сериализатор» держится на общем экземпляре, а не на вере.
        private static readonly JsonSerializerOptions Options = ProjectJson.Options;

        /// <summary>
        /// Возвращает независимую копию операции того же типа.
        /// </summary>
        /// <param name="source">Исходная операция.</param>
        /// <exception cref="ArgumentNullException">Операция не задана.</exception>
        /// <exception cref="InvalidOperationException">Копию не удалось создать.</exception>
        public static OperationBase Clone(OperationBase source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var type = source.GetType();
            var json = JsonSerializer.Serialize(source, type, Options);
            if (JsonSerializer.Deserialize(json, type, Options) is OperationBase clone)
            {
                // Копия представляет ту же операцию документа: идентификатор
                // переносится явно — в файл он не пишется, и сериализация
                // выдала бы копии новый.
                clone.Id = source.Id;
                return clone;
            }

            throw new InvalidOperationException($"Не удалось создать копию операции {type.Name}.");
        }

        /// <summary>
        /// Возвращает независимую копию операции, приведённую к типу вызывающего
        /// кода. Для интерфейсных типов (например, операции кармана) приведение
        /// безопасно: копия имеет тот же конкретный тип, что и оригинал.
        /// </summary>
        /// <typeparam name="T">Тип, к которому приводится копия.</typeparam>
        /// <param name="source">Исходная операция.</param>
        public static T Clone<T>(T source) where T : class
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (!(source is OperationBase operation))
                throw new InvalidOperationException($"Тип {typeof(T).Name} не является операцией.");

            return (T)(object)Clone(operation);
        }
    }
}
