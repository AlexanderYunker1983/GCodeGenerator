using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Служба сохранения/загрузки файлов проекта .ygc (пункты 0.6, 1.2 и 8.2 плана).
    /// Сериализация операций в JSON и разрешение типов по белому списку.
    /// Некорректные или более новые файлы отклоняются целиком, чтобы последующее
    /// сохранение не приводило к тихой потере неизвестных операций/секций.
    ///
    /// Текущий формат v3 (System.Text.Json):
    /// <code>{"version":3,"operations":[...],"format":{...},"spindle":{...},"coolant":{...},"workCoordinate":{...}}</code>
    /// — конверт camelCase; payload операций и настроек — PascalCase, как в модели.
    /// Все настройки, влияющие на генерацию, сохраняются вместе с проектом;
    /// UI-настройки остаются глобальными. Формат v2 читается для совместимости.
    ///
    /// Легаси-формат v1 (JavaScriptSerializer) — только чтение, мигрируется при сохранении:
    /// <code>{"Operations":[{"Type":"&lt;AssemblyQualifiedName&gt;","Data":"&lt;JSON операции&gt;"}]}</code>
    ///
    /// Старые .ygc (v1/v2) остаются читаемыми; сохранение — всегда v3.
    /// </summary>
    public class ProjectFileService : IProjectFileService
    {
        /// <summary>Текущая версия формата файла .ygc (поле "version").</summary>
        public const int CurrentVersion = 3;

        private static readonly JsonSerializerOptions PayloadOptions = new JsonSerializerOptions
        {
            Converters = { new DoubleJsonConverter(), new PrimitiveDictionaryConverter() }
        };

        /// <summary>
        /// Сериализует проект в JSON .ygc v3 (in-memory), включая все настройки,
        /// влияющие на генерацию G-code.
        /// </summary>
        /// <param name="operations">Операции в том порядке, в котором они должны сохраниться.</param>
        /// <param name="settings">Настройки генерации (null — секции не пишутся).</param>
        public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
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

        /// <summary>Сохраняет проект в файл в формате v3 (UTF-8 с BOM, как раньше).</summary>
        public void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу проекта не задан.", nameof(filePath));

            // Сначала полностью строим JSON в памяти, затем пишем временный файл
            // в том же каталоге и атомарно заменяем назначение. Ошибка сериализации
            // или записи не должна оставлять существующий .ygc частично обрезанным.
            var json = Serialize(operations, settings);
            var destinationPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(destinationPath);
            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(true));
                if (File.Exists(destinationPath))
                    File.Replace(temporaryPath, destinationPath, null);
                else
                    File.Move(temporaryPath, destinationPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Читает проект из файла (v3, v2 или легаси v1).
        /// <see cref="ProjectFileData.Operations">Operations</see> равно <c>null</c>, если в файле
        /// нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        public ProjectFileData Load(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return Deserialize(json);
        }

        /// <summary>
        /// Десериализует JSON проекта .ygc (v3, v2 или легаси v1).
        /// Отсутствующие секции настроек возвращаются как null.
        /// Бросает исключение при некорректном JSON.
        /// </summary>
        public ProjectFileData Deserialize(string json)
        {
            using var doc = JsonDocument.Parse(json); // бросает JsonException при некорректном JSON
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Файл проекта должен содержать JSON-объект.");

            // Версионный формат определяется наличием поля "version"; иначе — legacy v1.
            if (root.TryGetProperty("version", out var versionElement))
            {
                var version = ValidateVersionedEnvelope(root, versionElement);
                var operations = root.TryGetProperty("operations", out var operationsElement)
                    ? ReadOperationsArray(operationsElement, isVersioned: true)
                    : null;
                return new ProjectFileData
                {
                    Operations = operations,
                    Format = version >= 3 ? ReadSection<GCodeFormatSettings>(root, "format") : null,
                    Spindle = ReadSection<SpindleSettings>(root, "spindle"),
                    Coolant = ReadSection<CoolantSettings>(root, "coolant"),
                    WorkCoordinate = version >= 3
                        ? ReadSection<WorkCoordinateSettings>(root, "workCoordinate")
                        : null
                };
            }

            // Legacy v1: секций настроек нет по определению.
            if (!root.TryGetProperty("Operations", out var legacyOperationsElement))
                return new ProjectFileData();
            return new ProjectFileData
            {
                Operations = ReadOperationsArray(legacyOperationsElement, isVersioned: false)
            };
        }

        private static int ValidateVersionedEnvelope(JsonElement root, JsonElement versionElement)
        {
            if (versionElement.ValueKind != JsonValueKind.Number || !versionElement.TryGetInt32(out int version))
                throw new JsonException("Версия формата проекта должна быть целым числом.");

            if (version != 2 && version != CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Версия формата проекта {version} не поддерживается. Поддерживаются версии 2 и {CurrentVersion}.");
            }

            var allowedProperties = new HashSet<string>(StringComparer.Ordinal)
            {
                "version",
                "operations",
                "spindle",
                "coolant",
            };
            if (version >= 3)
            {
                allowedProperties.Add("format");
                allowedProperties.Add("workCoordinate");
            }
            var seenProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
            {
                if (!seenProperties.Add(property.Name))
                    throw new JsonException($"Поле проекта '{property.Name}' указано несколько раз.");
                if (!allowedProperties.Contains(property.Name))
                {
                    throw new NotSupportedException(
                        $"Файл проекта содержит неизвестную секцию '{property.Name}'.");
                }
            }

            return version;
        }

        private static T ReadSection<T>(JsonElement root, string sectionName) where T : class
        {
            if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind == JsonValueKind.Null)
                return null;
            if (section.ValueKind != JsonValueKind.Object)
                throw new JsonException($"Секция {sectionName} должна быть JSON-объектом.");
            return JsonSerializer.Deserialize<T>(section.GetRawText(), PayloadOptions);
        }

        private static List<OperationBase> ReadOperationsArray(JsonElement operationsElement, bool isVersioned)
        {
            if (operationsElement.ValueKind == JsonValueKind.Null)
                return null;

            if (operationsElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Секция операций должна быть массивом.");

            var result = new List<OperationBase>();
            int operationIndex = 0;
            foreach (var entry in operationsElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"Операция [{operationIndex}] должна быть JSON-объектом.");

                string typeName;
                string dataJson;

                if (isVersioned)
                {
                    if (!entry.TryGetProperty("type", out var typeElement)
                        || typeElement.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(typeElement.GetString()))
                    {
                        throw new JsonException($"У операции [{operationIndex}] отсутствует строковое поле type.");
                    }
                    typeName = typeElement.GetString();
                    if (!entry.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
                        throw new JsonException($"У операции [{operationIndex}] (type={typeName}) отсутствует поле data.");
                    if (dataElement.ValueKind != JsonValueKind.Object)
                        throw new JsonException($"Данные операции (type={typeName}) должны быть JSON-объектом.");
                    dataJson = dataElement.GetRawText();
                }
                else
                {
                    var rawType = entry.TryGetProperty("Type", out var legacyTypeElement) ? legacyTypeElement.GetString() : null;
                    typeName = ExtractLegacyClassName(rawType);
                    if (string.IsNullOrWhiteSpace(typeName))
                        throw new JsonException($"У legacy-операции [{operationIndex}] отсутствует строковое поле Type.");
                    if (!entry.TryGetProperty("Data", out var legacyDataElement) || legacyDataElement.ValueKind == JsonValueKind.Null)
                        throw new JsonException($"У legacy-операции [{operationIndex}] (Type={rawType}) отсутствует поле Data.");
                    if (legacyDataElement.ValueKind != JsonValueKind.String)
                        throw new JsonException($"Данные операции (Type={rawType}) в формате v1 должны быть JSON-строкой.");
                    dataJson = legacyDataElement.GetString();
                    if (string.IsNullOrWhiteSpace(dataJson))
                        throw new JsonException($"Данные legacy-операции [{operationIndex}] не могут быть пустыми.");
                }

                var type = OperationTypeNames.Resolve(typeName);
                if (type == null)
                {
                    throw new NotSupportedException(
                        $"Тип операции '{typeName}' в позиции [{operationIndex}] не поддерживается.");
                }

                // Валидный тип + не-объектный JSON данных — исключение (как в прежнем JavaScriptSerializer).
                var operation = JsonSerializer.Deserialize(dataJson, type, PayloadOptions) as OperationBase;
                if (operation == null)
                    throw new JsonException($"Не удалось прочитать операцию [{operationIndex}] (type={typeName}).");

                // Миграция легаси-Metadata в типизированные свойства (пункт 3.2 плана):
                // старые .ygc открываются, при сохранении Metadata уже не пишется.
                LegacyMetadataMigrator.Migrate(operation);

                result.Add(operation);
                operationIndex++;
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
