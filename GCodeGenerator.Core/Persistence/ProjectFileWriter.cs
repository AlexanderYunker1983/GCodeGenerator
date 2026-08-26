using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using GCodeGenerator.Models;

namespace GCodeGenerator.Persistence
{
    /// <summary>
    /// Запись проекта в текущий формат файла .ygc.
    ///
    /// Конверт пишется camelCase, содержимое операций и настроек —
    /// PascalCase, как в модели. Все настройки, влияющие на генерацию,
    /// сохраняются вместе с проектом; настройки внешнего вида остаются
    /// глобальными и в проект не попадают.
    ///
    /// Запись отделена от чтения намеренно: пишется всегда одна версия
    /// формата, а читается несколько, и держать оба занятия в одном классе
    /// значит делать его тем длиннее, чем больше версий накопилось.
    /// </summary>
    internal static class ProjectFileWriter
    {
        /// <summary>Версия формата, в которой сохраняется проект.</summary>
        public const int Version = 4;

        /// <summary>
        /// Настройки сериализации содержимого операций и секций настроек —
        /// общие с чтением, клоном и слепком (<see cref="ProjectJson"/>).
        /// Отдельный конвертер double не нужен: System.Text.Json пишет
        /// вещественные числа в кратчайшем round-trip-виде (0.3 → «0.3»),
        /// совпадающем с форматом «R».
        /// </summary>
        private static readonly JsonSerializerOptions PayloadOptions = ProjectJson.Options;

        /// <summary>
        /// Сериализует проект в JSON .ygc v4 (in-memory), включая все настройки,
        /// влияющие на генерацию G-code.
        /// </summary>
        /// <param name="operations">Операции в том порядке, в котором они должны сохраниться.</param>
        /// <param name="settings">Настройки генерации (null — секции не пишутся).</param>
        public static string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("version");
                writer.WriteNumberValue(Version);
                writer.WritePropertyName("operations");
                writer.WriteStartArray();
                if (operations != null)
                {
                    foreach (var op in operations)
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("type");
                        writer.WriteStringValue(OperationTypeNames.ToShortName(op.GetType()));
                        writer.WritePropertyName("data");
                        JsonSerializer.Serialize(writer, op, op.GetType(), PayloadOptions);
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();

                // UI-настройки намеренно не являются частью проекта.
                if (settings != null)
                {
                    writer.WritePropertyName("format");
                    JsonSerializer.Serialize(writer, settings.Format, PayloadOptions);
                    writer.WritePropertyName("spindle");
                    JsonSerializer.Serialize(writer, settings.Spindle, PayloadOptions);
                    writer.WritePropertyName("coolant");
                    JsonSerializer.Serialize(writer, settings.Coolant, PayloadOptions);
                    writer.WritePropertyName("workCoordinate");
                    JsonSerializer.Serialize(writer, settings.WorkCoordinate, PayloadOptions);
                }

                writer.WriteEndObject();
                writer.Flush();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
