using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Служба сохранения/загрузки файлов проекта .ygc (пункты 0.6 и 1.2 плана).
    /// Сериализация операций в JSON, разрешение типов по белому списку, пропуск некорректных записей.
    ///
    /// Формат v2 (System.Text.Json), в него всегда сохраняется:
    /// <code>{"version":2,"operations":[{"type":"&lt;короткое имя&gt;","data":{...}}]}</code>
    /// — конверт camelCase; данные операции (payload) — PascalCase, как в модели (как в v1).
    ///
    /// Легаси-формат v1 (JavaScriptSerializer) — только чтение, мигрируется при сохранении:
    /// <code>{"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}</code>
    ///
    /// Старые .ygc (v1) остаются читаемыми; сохранение — всегда v2.
    /// </summary>
    public class ProjectFileService : IProjectFileService
    {
        /// <summary>Текущая версия формата файла .ygc (поле "version").</summary>
        public const int CurrentVersion = 2;

        private static readonly JsonSerializerOptions PayloadOptions = new JsonSerializerOptions
        {
            Converters = { new DoubleJsonConverter(), new PrimitiveDictionaryConverter() }
        };

        /// <summary>
        /// Сериализует операции в JSON проекта .ygc v2 (in-memory).
        /// </summary>
        /// <param name="operations">Операции в том порядке, в котором они должны сохраниться.</param>
        public string Serialize(IReadOnlyList<OperationBase> operations)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("version");
                writer.WriteNumberValue(CurrentVersion);
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
                writer.WriteEndObject();
                writer.Flush();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        /// <summary>Сохраняет операции в файл в формате v2 (UTF-8 с BOM, как раньше).</summary>
        public void Save(string filePath, IReadOnlyList<OperationBase> operations)
        {
            File.WriteAllText(filePath, Serialize(operations), new UTF8Encoding(true));
        }

        /// <summary>
        /// Читает проект из файла (v2 или легаси v1).
        /// Возвращает <c>null</c>, если в файле нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        public List<OperationBase> Load(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return Deserialize(json);
        }

        /// <summary>
        /// Десериализует JSON проекта .ygc (v2 или легаси v1) в список операций.
        /// Возвращает <c>null</c>, если нет секции операций. Бросает исключение при некорректном JSON.
        /// </summary>
        public List<OperationBase> Deserialize(string json)
        {
            using var doc = JsonDocument.Parse(json); // бросает JsonException при некорректном JSON
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Файл проекта должен содержать JSON-объект.");

            // v2 определяется наличием поля "version"; иначе — легаси v1.
            if (root.TryGetProperty("version", out _))
            {
                if (!root.TryGetProperty("operations", out var operationsElement))
                    return null;
                return ReadOperationsArray(operationsElement, isV2: true);
            }

            if (!root.TryGetProperty("Operations", out var legacyOperationsElement))
                return null;
            return ReadOperationsArray(legacyOperationsElement, isV2: false);
        }

        private static List<OperationBase> ReadOperationsArray(JsonElement operationsElement, bool isV2)
        {
            if (operationsElement.ValueKind == JsonValueKind.Null)
                return null;

            if (operationsElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Секция операций должна быть массивом.");

            var result = new List<OperationBase>();
            foreach (var entry in operationsElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue; // некорректная запись — пропускаем

                string typeName;
                string dataJson;

                if (isV2)
                {
                    typeName = entry.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
                    if (!entry.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
                        continue; // нет данных — пропускаем
                    if (dataElement.ValueKind != JsonValueKind.Object)
                        throw new JsonException($"Данные операции (type={typeName}) должны быть JSON-объектом.");
                    dataJson = dataElement.GetRawText();
                }
                else
                {
                    var rawType = entry.TryGetProperty("Type", out var legacyTypeElement) ? legacyTypeElement.GetString() : null;
                    typeName = ExtractLegacyClassName(rawType);
                    if (!entry.TryGetProperty("Data", out var legacyDataElement) || legacyDataElement.ValueKind == JsonValueKind.Null)
                        continue; // нет данных — пропускаем
                    if (legacyDataElement.ValueKind != JsonValueKind.String)
                        throw new JsonException($"Данные операции (Type={rawType}) в формате v1 должны быть JSON-строкой.");
                    dataJson = legacyDataElement.GetString();
                    if (string.IsNullOrWhiteSpace(dataJson))
                        continue;
                }

                var type = OperationTypeNames.Resolve(typeName);
                if (type == null)
                    continue; // пустой/неизвестный тип — пропускаем

                // Валидный тип + не-объектный JSON данных — исключение (как в прежнем JavaScriptSerializer).
                var operation = JsonSerializer.Deserialize(dataJson, type, PayloadOptions) as OperationBase;
                if (operation == null)
                    continue;

                // Миграция легаси-Metadata в типизированные свойства (пункт 3.2 плана):
                // старые .ygc открываются, при сохранении Metadata уже не пишется.
                LegacyMetadataMigrator.Migrate(operation);

                result.Add(operation);
            }

            return result;
        }

        /// <summary>
        /// Извлекает имя класса из AssemblyQualifiedName (формат v1):
        /// "GCodeGenerator.Models.DrillPointsOperation, GCodeGenerator, Version=..." → "DrillPointsOperation".
        /// Версия сборки игнорируется (устраняет уязвимость версий из п. 0.7).
        /// </summary>
        private static string ExtractLegacyClassName(string assemblyQualifiedName)
        {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedName))
                return null;

            var commaIndex = assemblyQualifiedName.IndexOf(',');
            var typeName = commaIndex >= 0
                ? assemblyQualifiedName.Substring(0, commaIndex)
                : assemblyQualifiedName;
            typeName = typeName.Trim();

            var dotIndex = typeName.LastIndexOf('.');
            return dotIndex >= 0 ? typeName.Substring(dotIndex + 1) : typeName;
        }
    }
}
