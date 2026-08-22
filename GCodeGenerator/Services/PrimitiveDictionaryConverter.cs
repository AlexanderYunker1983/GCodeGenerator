using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Конвертер <c>Dictionary&lt;string, object&gt;</c> (поле <c>Metadata</c> операций)
    /// для System.Text.Json. Точно повторяет поведение JavaScriptSerializer (формат v1),
    /// чтобы значения Metadata после загрузки были идентичны прежним:
    /// <list type="bullet">
    ///   <item>чтение: целое → <c>Int32</c>/<c>Int64</c>, дробное → <c>Decimal</c>,
    ///         строка → <c>string</c>, bool → <c>bool</c>, null → <c>null</c>;</item>
    ///   <item>запись: по фактическому типу значения (double/int/string/bool/enum/null).</item>
    /// </list>
    /// Критично: enum-значения (<c>ToolPathMode</c>, <c>PocketStrategy</c>, ...) читаются как
    /// <c>Int32</c> — VM приводят их прямым кастом, например
    /// <c>(MillingDirection)operation.Metadata["Direction"]</c>, что требует целочисленного типа.
    /// </summary>
    public sealed class PrimitiveDictionaryConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Ожидался объект для Dictionary<string, object>, получено: {reader.TokenType}.");

            var result = new Dictionary<string, object>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return result;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException($"Ожидалось имя свойства в Dictionary<string, object>, получено: {reader.TokenType}.");

                var key = reader.GetString();
                reader.Read();
                result[key] = ReadValue(ref reader);
            }

            throw new JsonException("Неожиданный конец JSON в Dictionary<string, object>.");
        }

        private static object ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    // Повторяем JavaScriptSerializer: целое → Int32, затем Int64, иначе → Decimal.
                    if (reader.TryGetInt32(out var i32)) return i32;
                    if (reader.TryGetInt64(out var i64)) return i64;
                    return reader.GetDecimal();
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Неподдерживаемый тип значения в Metadata: {reader.TokenType}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            foreach (var kv in value)
            {
                writer.WritePropertyName(kv.Key);
                WriteValue(writer, kv.Value);
            }
            writer.WriteEndObject();
        }

        private static void WriteValue(Utf8JsonWriter writer, object value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case double d:
                    // Краткий round-trip-вид (как в v1), а не многословный G17 .NET Framework.
                    writer.WriteRawValue(JsonNumbers.FormatDouble(d));
                    break;
                case float f:
                    writer.WriteNumberValue(f);
                    break;
                case decimal m:
                    writer.WriteNumberValue(m);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case short sh:
                    writer.WriteNumberValue(sh);
                    break;
                case ushort ush:
                    writer.WriteNumberValue(ush);
                    break;
                case byte by:
                    writer.WriteNumberValue(by);
                    break;
                case sbyte sb:
                    writer.WriteNumberValue(sb);
                    break;
                case uint ui:
                    writer.WriteNumberValue(ui);
                    break;
                case ulong ul:
                    writer.WriteNumberValue(ul);
                    break;
                case System.Enum e:
                    // Как JavaScriptSerializer: enum пишется как его целое значение.
                    writer.WriteNumberValue(Convert.ToInt32(e, CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new NotSupportedException(
                        $"Неподдерживаемый тип значения в Metadata: {value.GetType().FullName}.");
            }
        }
    }
}
