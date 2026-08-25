using System;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Имена типов операций в файле проекта.
    ///
    /// Перечень типов больше не дублируется здесь: имена берутся из
    /// <see cref="OperationCatalog"/>, где тип операции описан один раз.
    /// Разрешение имени остаётся белым списком — тип, отсутствующий
    /// в каталоге, не будет создан по данным из файла.
    /// </summary>
    public static class OperationTypeNames
    {
        /// <summary>
        /// Разрешает имя операции (имя из каталога или имя класса из первой
        /// версии формата) в тип. Возвращает <c>null</c>, если имя пустое или
        /// неизвестно; загрузчик проекта трактует это как неподдерживаемый файл
        /// и не открывает его частично.
        /// </summary>
        public static Type Resolve(string name)
            => OperationCatalog.FindByPersistentName(name)?.OperationType;

        /// <summary>
        /// Имя типа операции для записи в файл проекта.
        /// Бросает <see cref="NotSupportedException"/> для типа вне каталога —
        /// громкий сбой при сохранении вместо тихой потери операции.
        /// </summary>
        public static string ToShortName(Type type)
            => OperationCatalog.ForType(type).PersistentName;
    }
}
