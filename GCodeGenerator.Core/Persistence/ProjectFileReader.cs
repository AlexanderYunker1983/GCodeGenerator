using System;
using System.Collections.Generic;
using System.Text.Json;
using GCodeGenerator.Models;

namespace GCodeGenerator.Persistence
{
    /// <summary>
    /// Чтение проекта из файла .ygc: версии 2, 3 и 4.
    ///
    /// Некорректные и более новые файлы отклоняются целиком: иначе
    /// последующее сохранение тихо потеряло бы операции и секции, которых
    /// эта сборка не понимает. Формат первой версии, писавшийся прежним
    /// сериализатором, больше не читается — файл без поля версии
    /// отклоняется с объяснением.
    /// </summary>
    internal static class ProjectFileReader
    {
        /// <summary>Настройки чтения содержимого операций и секций настроек.</summary>
        // Общие настройки сериализации: чтение обязано разбирать ровно то
        // представление, которое пишет ProjectFileWriter (см. ProjectJson).
        private static readonly JsonSerializerOptions PayloadOptions = ProjectJson.Options;

        /// <summary>
        /// Десериализует JSON проекта .ygc (v4, v3 или v2).
        /// Отсутствующие секции настроек возвращаются как null.
        /// Бросает исключение при некорректном JSON.
        /// </summary>
        public static ProjectFileData Deserialize(string json)
        {
            using var doc = JsonDocument.Parse(json); // бросает JsonException при некорректном JSON
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new JsonException("Файл проекта должен содержать JSON-объект.");

            if (!root.TryGetProperty("version", out var versionElement))
            {
                throw new NotSupportedException(
                    "Файл проекта не содержит версии формата. Так выглядят файлы первой версии, "
                    + "которые больше не читаются: откройте такой файл прежней сборкой программы "
                    + "и пересохраните.");
            }

            var version = ValidateVersionedEnvelope(root, versionElement);
            var operations = root.TryGetProperty("operations", out var operationsElement)
                ? ReadOperationsArray(operationsElement)
                : null;
            return new ProjectFileData
            {
                Version = version,
                Operations = operations,
                Format = version >= 3 ? ReadSection<GCodeFormatSettings>(root, "format") : null,
                Spindle = ReadSection<SpindleSettings>(root, "spindle"),
                Coolant = ReadSection<CoolantSettings>(root, "coolant"),
                WorkCoordinate = version >= 3
                    ? ReadSection<WorkCoordinateSettings>(root, "workCoordinate")
                    : null
            };
        }

        private static int ValidateVersionedEnvelope(JsonElement root, JsonElement versionElement)
        {
            if (versionElement.ValueKind != JsonValueKind.Number || !versionElement.TryGetInt32(out int version))
                throw new JsonException("Версия формата проекта должна быть целым числом.");

            if (version < 2 || version > ProjectFileWriter.Version)
            {
                throw new NotSupportedException(
                    $"Версия формата проекта {version} не поддерживается. Поддерживаются версии 2-{ProjectFileWriter.Version}.");
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

        private static List<OperationBase> ReadOperationsArray(JsonElement operationsElement)
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

                if (!entry.TryGetProperty("type", out var typeElement)
                    || typeElement.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(typeElement.GetString()))
                {
                    throw new JsonException($"У операции [{operationIndex}] отсутствует строковое поле type.");
                }
                var typeName = typeElement.GetString();
                if (!entry.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
                    throw new JsonException($"У операции [{operationIndex}] (type={typeName}) отсутствует поле data.");
                if (dataElement.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"Данные операции (type={typeName}) должны быть JSON-объектом.");
                var dataJson = dataElement.GetRawText();

                var type = OperationTypeNames.Resolve(typeName);
                if (type == null)
                {
                    throw new NotSupportedException(
                        $"Тип операции '{typeName}' в позиции [{operationIndex}] не поддерживается.");
                }

                using var payloadDocument = JsonDocument.Parse(dataJson);
                var payload = payloadDocument.RootElement;
                if (payload.ValueKind != JsonValueKind.Object)
                    throw new JsonException($"Данные операции [{operationIndex}] (type={typeName}) должны быть JSON-объектом.");

                // Валидный тип + не-объектный JSON данных — исключение (как в прежнем JavaScriptSerializer).
                var operation = JsonSerializer.Deserialize(payload.GetRawText(), type, PayloadOptions) as OperationBase;
                if (operation == null)
                    throw new JsonException($"Не удалось прочитать операцию [{operationIndex}] (type={typeName}).");

                RejectCurrentMetadata(payload, typeName, operationIndex);

                result.Add(operation);
                operationIndex++;
            }

            return result;
        }

        private static void RejectCurrentMetadata(JsonElement payload, string typeName, int operationIndex)
        {
            if (TryGetSingleMetadata(payload, typeName, operationIndex, out _))
            {
                throw new NotSupportedException(
                    $"Операция [{operationIndex}] (type={typeName}) содержит удалённое поле Metadata, "
                    + "которое не поддерживается форматом v4.");
            }
        }

        private static bool TryGetSingleMetadata(
            JsonElement payload,
            string typeName,
            int operationIndex,
            out JsonElement metadata)
        {
            metadata = default;
            var found = false;
            foreach (var property in payload.EnumerateObject())
            {
                if (!string.Equals(property.Name, "Metadata", StringComparison.Ordinal))
                    continue;

                if (found)
                {
                    throw new JsonException(
                        $"Поле Metadata операции [{operationIndex}] (type={typeName}) указано несколько раз.");
                }

                metadata = property.Value;
                found = true;
            }

            return found;
        }
    }
}
