#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        private static readonly JsonSerializerOptions PayloadOptions = new JsonSerializerOptions(ProjectJson.Options)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        /// <summary>
        /// Десериализует JSON проекта .ygc (v4, v3 или v2).
        /// Отсутствующие секции настроек возвращаются как null.
        /// Бросает исключение при некорректном JSON.
        /// </summary>
        public static ProjectFileData Deserialize(string json)
        {
            // Отказы адресованы пользователю, поэтому идут кодами CoreException:
            // нейтральный английский — в журнал, перевод подставляет интерфейс.
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException parseFailure)
            {
                throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                    "The project file is damaged or has an unexpected structure ({0}).",
                    parseFailure.Message);
            }

            using var document = doc;
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                    "The project file is damaged or has an unexpected structure ({0}).",
                    "the root is not a JSON object");
            }

            if (!root.TryGetProperty("version", out var versionElement))
            {
                // Так выглядят файлы первой версии, которые больше не читаются.
                throw new CoreException(CoreErrorCodes.ProjectFileLegacyVersion,
                    "The project file has no format version: first-format files are no longer readable. "
                    + "Open the file with an earlier build of the program and save it again.");
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
            {
                throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                    "The project file is damaged or has an unexpected structure ({0}).",
                    "the format version is not an integer");
            }

            if (version < 2 || version > ProjectFileWriter.Version)
            {
                throw new CoreException(CoreErrorCodes.ProjectFileUnsupportedVersion,
                    "The project file uses format version {0}; this build supports versions {1} through {2}.",
                    version, 2, ProjectFileWriter.Version);
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
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"the field '{property.Name}' occurs more than once"));
                }

                if (!allowedProperties.Contains(property.Name))
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileUnknownSection,
                        "The project file contains an unknown section '{0}': it was probably saved "
                        + "by a newer version of the program.",
                        property.Name);
                }
            }

            return version;
        }

        private static T? ReadSection<T>(JsonElement root, string sectionName) where T : class
        {
            if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind == JsonValueKind.Null)
                return null;
            if (section.ValueKind != JsonValueKind.Object)
            {
                throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                    "The project file is damaged or has an unexpected structure ({0}).",
                    FormattableString.Invariant($"the section '{sectionName}' is not a JSON object"));
            }
            RejectDuplicateProperties(section, sectionName);
            try
            {
                return JsonSerializer.Deserialize<T>(section.GetRawText(), PayloadOptions);
            }
            catch (JsonException failure)
            {
                throw Corrupt(FormattableString.Invariant(
                    $"section '{sectionName}' contains unsupported or invalid data: {failure.Message}"));
            }
        }

        private static List<OperationBase>? ReadOperationsArray(JsonElement operationsElement)
        {
            if (operationsElement.ValueKind == JsonValueKind.Null)
                return null;

            if (operationsElement.ValueKind != JsonValueKind.Array)
            {
                throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                    "The project file is damaged or has an unexpected structure ({0}).",
                    "the operations section is not an array");
            }

            if (operationsElement.GetArrayLength() > GenerationLimits.MaxOperations)
            {
                throw new CoreException(
                    CoreErrorCodes.ProjectFileTooComplex,
                    "The project contains more than the supported maximum of {0} operations.",
                    GenerationLimits.MaxOperations);
            }

            var result = new List<OperationBase>(operationsElement.GetArrayLength());
            int operationIndex = 0;
            foreach (var entry in operationsElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation [{operationIndex}] is not a JSON object"));
                }

                ValidateOperationEnvelope(entry, operationIndex);

                var typeName = entry.TryGetProperty("type", out var typeElement)
                    && typeElement.ValueKind == JsonValueKind.String
                    ? typeElement.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation [{operationIndex}] is missing the string field 'type'"));
                }

                if (!entry.TryGetProperty("data", out var dataElement) || dataElement.ValueKind == JsonValueKind.Null)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation [{operationIndex}] ({typeName}) is missing the 'data' field"));
                }

                if (dataElement.ValueKind != JsonValueKind.Object)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation data ({typeName}) is not a JSON object"));
                }

                var dataJson = dataElement.GetRawText();

                var type = OperationTypeNames.Resolve(typeName);
                if (type == null)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileUnknownOperationType,
                        "The operation type '{0}' (position {1}) is not supported by this build.",
                        typeName, operationIndex);
                }

                using var payloadDocument = JsonDocument.Parse(dataJson);
                var payload = payloadDocument.RootElement;
                if (payload.ValueKind != JsonValueKind.Object)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation data [{operationIndex}] ({typeName}) is not a JSON object"));
                }

                RejectDuplicateProperties(payload,
                    FormattableString.Invariant($"operation [{operationIndex}] ({typeName})"));

                OperationBase? operation;
                try
                {
                    operation = JsonSerializer.Deserialize(payload.GetRawText(), type, PayloadOptions) as OperationBase;
                }
                catch (JsonException failure)
                {
                    throw Corrupt(FormattableString.Invariant(
                        $"operation [{operationIndex}] ({typeName}) contains unsupported or invalid data: {failure.Message}"));
                }
                if (operation == null)
                {
                    throw new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                        "The project file is damaged or has an unexpected structure ({0}).",
                        FormattableString.Invariant($"operation [{operationIndex}] ({typeName}) could not be read"));
                }

                result.Add(operation);
                operationIndex++;
            }

            return result;
        }

        private static void ValidateOperationEnvelope(JsonElement entry, int operationIndex)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in entry.EnumerateObject())
            {
                if (!seen.Add(property.Name))
                    throw Corrupt(FormattableString.Invariant(
                        $"operation [{operationIndex}] field '{property.Name}' occurs more than once"));
                if (property.Name != "type" && property.Name != "data")
                    throw Corrupt(FormattableString.Invariant(
                        $"operation [{operationIndex}] contains unsupported field '{property.Name}'"));
            }
        }

        private static void RejectDuplicateProperties(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!seen.Add(property.Name))
                        throw Corrupt(FormattableString.Invariant(
                            $"{path} field '{property.Name}' occurs more than once"));
                    RejectDuplicateProperties(property.Value, path + "." + property.Name);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    RejectDuplicateProperties(item, FormattableString.Invariant($"{path}[{index++}]"));
            }
        }

        private static CoreException Corrupt(string detail)
            => new CoreException(CoreErrorCodes.ProjectFileCorrupt,
                "The project file is damaged or has an unexpected structure ({0}).", detail);

    }
}
