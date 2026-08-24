using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.6 плана: служба файлов проекта .ygc через IoC
    /// (из MainViewModel убрано <c>new ProjectFileService()</c>).
    /// Пункт 8.2 плана (D4): при сохранении в файл пишутся секции
    /// spindle/coolant; при чтении возвращаются в <see cref="ProjectFileData"/>.
    /// </summary>
    public interface IProjectFileService
    {
        /// <summary>
        /// Сохраняет проект в файл в формате v2 (UTF-8 с BOM), включая
        /// секции spindle/coolant из <paramref name="settings"/>.
        /// </summary>
        void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings);

        /// <summary>
        /// Читает проект из файла (v2 или легаси v1).
        /// <see cref="ProjectFileData.Operations"/> равно <c>null</c>, если в файле
        /// нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        ProjectFileData Load(string filePath);
    }
}
