#nullable enable
using System;
using System.IO;
using System.Text;

namespace GCodeGenerator.Services
{
    public sealed class GCodeFileService : IGCodeFileService
    {
        // UTF-8 без BOM. Encoding.UTF8 ставит в начало файла преамбулу
        // EF BB BF — три байта перед первым кадром, которые часть стоек
        // не понимает: программа собрана из чистого ASCII, а сам файл
        // начинался не с него. Просто ASCII-кодировкой заменить нельзя:
        // имя операции пишет пользователь, продукт передаёт его как есть,
        // и не-ASCII имя превратилось бы в вопросительные знаки.
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public void Save(string filePath, string gCode)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("The G-code file path is not set.", nameof(filePath));

            var destinationPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(destinationPath)
                ?? throw new ArgumentException("The G-code file path points to a filesystem root.", nameof(filePath));
            var temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
            var backupPath = destinationPath + ".bak";

            try
            {
                // Программа сначала целиком попадает в соседний временный
                // файл. Flush(true) доводит её до устройства до того, как
                // имя назначения начнёт указывать на новую версию: успешный
                // Save означает, что на диске есть полный файл с эпилогом.
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           bufferSize: 4096,
                           FileOptions.WriteThrough))
                {
                    using (var writer = new StreamWriter(
                               stream,
                               Utf8WithoutBom,
                               bufferSize: 4096,
                               leaveOpen: true))
                    {
                        writer.Write(gCode ?? string.Empty);
                        writer.Flush();
                    }

                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(destinationPath))
                    File.Replace(temporaryPath, destinationPath, backupPath);
                else
                    File.Move(temporaryPath, destinationPath);
            }
            catch
            {
                // Очистка вспомогательного файла не имеет права заменить
                // исходную ошибку записи или атомарной замены своей ошибкой.
                TryDeleteTemporary(temporaryPath);
                throw;
            }
        }

        private static void TryDeleteTemporary(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
                // Исходное исключение важнее ошибки уборки. Файл имеет
                // уникальное скрытое имя и не подменяет программу станка.
            }
        }
    }
}
