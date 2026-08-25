using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.6 плана: служба файлов проекта .ygc через IoC
    /// (из MainViewModel убрано <c>new ProjectFileService()</c>).
    /// При сохранении в файл пишутся все настройки генерации; при чтении они
    /// возвращаются в <see cref="ProjectFileData"/>.
    /// </summary>
    public interface IProjectFileService
    {
        /// <summary>
        /// Сохраняет проект в текущем формате (UTF-8 с BOM), включая настройки
        /// генерации из <paramref name="settings"/>.
        /// </summary>
        void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings);

        /// <summary>
        /// Читает проект из файла (v4, v3, v2 или легаси v1).
        /// <see cref="ProjectFileData.Operations"/> равно <c>null</c>, если в файле
        /// нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        ProjectFileData Load(string filePath);
    }
}
