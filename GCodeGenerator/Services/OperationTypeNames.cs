using System;
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Явный реестр типов операций для сериализации .ygc (пункт 1.2 плана).
    /// Белый список вместо <c>AssemblyQualifiedName</c> (устраняет уязвимость версий сборки,
    /// зафиксированную в п. 0.7): короткие имена для формата v2 + имена классов для
    /// легаси-файлов v1. Разрешение — без учёта регистра.
    /// </summary>
    public static class OperationTypeNames
    {
        /// <summary>Короткие имена (дискриминатор v2, поле "type") для каждого типа операции.</summary>
        private static readonly Dictionary<Type, string> ShortNames = new Dictionary<Type, string>
        {
            { typeof(DrillPointsOperation), "DrillPoints" },
            { typeof(ProfileRectangleOperation), "ProfileRectangle" },
            { typeof(ProfileRoundedRectangleOperation), "ProfileRoundedRectangle" },
            { typeof(ProfileCircleOperation), "ProfileCircle" },
            { typeof(ProfileEllipseOperation), "ProfileEllipse" },
            { typeof(ProfilePolygonOperation), "ProfilePolygon" },
            { typeof(ProfileDxfOperation), "ProfileDxf" },
            { typeof(PocketRectangleOperation), "PocketRectangle" },
            { typeof(PocketCircleOperation), "PocketCircle" },
            { typeof(PocketEllipseOperation), "PocketEllipse" },
            { typeof(PocketDxfOperation), "PocketDxf" },
        };

        /// <summary>Все допустимые имена (короткие v2 + имена классов v1) → тип, без учёта регистра.</summary>
        private static readonly Dictionary<string, Type> Names = BuildNameMap();

        private static Dictionary<string, Type> BuildNameMap()
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in ShortNames)
            {
                map[kv.Value] = kv.Key;    // короткое имя (v2)
                map[kv.Key.Name] = kv.Key; // имя класса (v1-легаси), например "DrillPointsOperation"
            }
            return map;
        }

        /// <summary>
        /// Разрешает имя операции (короткое v2 или имя класса v1) в тип.
        /// Возвращает <c>null</c>, если имя пустое или неизвестно; загрузчик проекта
        /// трактует это как неподдерживаемый файл и не открывает его частично.
        /// </summary>
        public static Type Resolve(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            return Names.TryGetValue(name.Trim(), out var type) ? type : null;
        }

        /// <summary>
        /// Короткое имя (v2) для типа операции.
        /// Бросает <see cref="NotSupportedException"/> для незарегистрированных типов —
        /// громкий сбой при сохранении вместо тихой потери операции.
        /// </summary>
        public static string ToShortName(Type type)
        {
            if (ShortNames.TryGetValue(type, out var name))
                return name;
            throw new NotSupportedException($"Тип {type.FullName} не зарегистрирован в OperationTypeNames.");
        }
    }
}
