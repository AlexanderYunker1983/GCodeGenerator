using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Форматирование чисел для .ygc. System.Text.Json на .NET Framework пишет double
    /// в многословном виде (0.3 → "0.29999999999999999"); формат "R" даёт краткое
    /// round-trip-представление (0.3 → "0.3"), как в прежнем JavaScriptSerializer.
    /// </summary>
    internal static class JsonNumbers
    {
        public static string FormatDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new NotSupportedException("NaN/Infinity не поддерживаются в файле проекта .ygc.");
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Конвертер <c>double</c> для System.Text.Json: запись в кратком round-trip-виде
    /// (формат "R"), чтение — стандартное. Применяется к числовым свойствам операций.
    /// </summary>
    public sealed class DoubleJsonConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDouble();

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
            => writer.WriteRawValue(JsonNumbers.FormatDouble(value));
    }
}
