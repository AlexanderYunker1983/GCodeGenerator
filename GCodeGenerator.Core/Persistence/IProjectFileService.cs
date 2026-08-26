#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Models;

namespace GCodeGenerator.Persistence
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
        /// генерации из <paramref name="settings"/>. Запись атомарна: ошибка
        /// не оставляет существующий файл обрезанным.
        /// </summary>
        void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings? settings);

        /// <summary>
        /// Сериализует проект в текст текущего формата — первую стадию
        /// сохранения, выполняемую на потоке интерфейса: документ нельзя
        /// читать из фона, пока его может править пользователь.
        /// </summary>
        string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings? settings);

        /// <summary>
        /// Записывает уже сериализованный проект — вторую стадию сохранения,
        /// пригодную для фонового потока. Запись атомарна, как у
        /// <see cref="Save"/>.
        /// </summary>
        void SaveSerialized(string filePath, string json);

        /// <summary>
        /// Читает проект из файла (v4, v3 или v2).
        /// <see cref="ProjectFileData.Operations"/> равно <c>null</c>, если в файле
        /// нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        ProjectFileData Load(string filePath);
    }
}
