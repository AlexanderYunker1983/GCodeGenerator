#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GCodeGenerator.Models;

namespace GCodeGenerator.Persistence
{
    /// <summary>
    /// Файл проекта .ygc: чтение и запись на диске.
    ///
    /// Сам формат этот класс не знает — им заняты
    /// <see cref="ProjectFileWriter"/> и <see cref="ProjectFileReader"/>.
    /// Здесь остаётся работа с файлом: атомарная замена при сохранении,
    /// чтение в нужной кодировке и проверка пути. Прежде все три занятия
    /// жили вместе, и следующая версия формата стала бы ещё одной веткой
    /// в том же классе.
    /// </summary>
    public class ProjectFileService : IProjectFileService
    {
        /// <summary>Текущая версия формата файла .ygc (поле "version").</summary>
        public const int CurrentVersion = ProjectFileWriter.Version;

        /// <summary>
        /// Сериализует проект в JSON текущего формата, включая все настройки,
        /// влияющие на генерацию G-code.
        /// </summary>
        /// <param name="operations">Операции в том порядке, в котором они должны сохраниться.</param>
        /// <param name="settings">Настройки генерации (null — секции не пишутся).</param>
        public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings? settings)
            => ProjectFileWriter.Serialize(operations, settings);

        /// <summary>
        /// Разбирает JSON проекта .ygc (версии 2, 3 и 4).
        /// </summary>
        /// <param name="json">Содержимое файла проекта.</param>
        public ProjectFileData Deserialize(string json)
            => ProjectFileReader.Deserialize(json);

        /// <summary>Сохраняет проект в файл (UTF-8 с BOM, как раньше).</summary>
        public void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings? settings)
            => SaveSerialized(filePath, Serialize(operations, settings));

        /// <summary>
        /// Записывает уже сериализованный проект. Разделение стадий нужно
        /// асинхронному сохранению: слепок снимается на потоке интерфейса,
        /// а на диск — самую долгую часть — текст пишет фоновый поток.
        /// </summary>
        /// <param name="filePath">Путь к файлу проекта.</param>
        /// <param name="json">Содержимое, полученное от <see cref="Serialize"/>.</param>
        public void SaveSerialized(string filePath, string json)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("The project file path is not set.", nameof(filePath));
            if (json == null)
                throw new ArgumentNullException(nameof(json));

            // JSON уже построен целиком; пишем временный файл в том же
            // каталоге и атомарно заменяем назначение. Предыдущая успешная
            // версия остаётся рядом как .bak: атомарность защищает от
            // оборванной записи, резервная копия — от ошибочного сохранения
            // корректного, но нежелательного состояния.
            var destinationPath = Path.GetFullPath(filePath);
            // Каталога нет только у корня файловой системы — файлом проекта
            // такой путь быть не может.
            var directory = Path.GetDirectoryName(destinationPath)
                ?? throw new ArgumentException("The project file path points to a filesystem root.", nameof(filePath));
            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
            var backupPath = destinationPath + ".bak";

            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(true));
                if (File.Exists(destinationPath))
                    File.Replace(temporaryPath, destinationPath, backupPath);
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
        /// Читает проект из файла (версии 2, 3 и 4).
        /// <see cref="ProjectFileData.Operations">Operations</see> равно <c>null</c>, если в файле
        /// нет секции операций (пустой/чужой файл).
        /// Бросает исключение при некорректном JSON — обработчик ошибки остаётся у вызывающего.
        /// </summary>
        public ProjectFileData Load(string filePath)
        {
            var json = File.ReadAllText(filePath, Encoding.UTF8);
            return Deserialize(json);
        }
    }
}
