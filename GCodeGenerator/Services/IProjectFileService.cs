using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.6 плана: служба файлов проекта .ygc через IoC
    /// (из MainViewModel убрано <c>new ProjectFileService()</c>).
    /// </summary>
    public interface IProjectFileService
    {
        /// <summary>Сохраняет операции в файл в формате v2 (UTF-8 с BOM).</summary>
        void Save(string filePath, IReadOnlyList<OperationBase> operations);

        /// <summary>
        /// Читает проект из файла (v2 или легаси v1).
        /// Возвращает <c>null</c>, если в файле нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        List<OperationBase> Load(string filePath);
    }
}
