using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Read-only adapter for the primitive dictionaries written by legacy
    /// JavaScriptSerializer projects. It is registered only on the v1-v3
    /// compatibility path and cannot participate in current project writes.
    /// </summary>
    internal sealed class LegacyMetadataDictionaryConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Ожидался объект Metadata, получено: {reader.TokenType}.");

            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return result;
                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException($"Ожидалось имя свойства Metadata, получено: {reader.TokenType}.");

                var key = reader.GetString();
                if (!reader.Read())
                    throw new JsonException("Неожиданный конец JSON в Metadata.");
                if (result.ContainsKey(key))
                    throw new JsonException($"Ключ Metadata '{key}' указан несколько раз.");

                result.Add(key, ReadValue(ref reader));
            }

            throw new JsonException("Неожиданный конец JSON в Metadata.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            Dictionary<string, object> value,
            JsonSerializerOptions options)
        {
            throw new NotSupportedException("Legacy Metadata доступно только для чтения.");
        }

        private static object ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    // Повторяет JavaScriptSerializer: Int32, затем Int64,
                    // для остальных чисел — Decimal.
                    if (reader.TryGetInt32(out var int32))
                        return int32;
                    if (reader.TryGetInt64(out var int64))
                        return int64;
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
                    throw new JsonException(
                        $"Неподдерживаемый тип значения в Metadata: {reader.TokenType}.");
            }
        }
    }
}
