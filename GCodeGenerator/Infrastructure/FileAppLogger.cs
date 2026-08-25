using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using GCodeGenerator.Diagnostics;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Файловый журнал приложения: пишет строки в
    /// <c>%LOCALAPPDATA%\GCodeGenerator\logs\gcodegenerator.log</c>.
    ///
    /// Ротация — по размеру: при превышении <see cref="MaxFileSizeBytes"/>
    /// текущий файл переименовывается в <c>gcodegenerator.1.log</c> (предыдущий
    /// архив удаляется), поэтому журнал занимает не более двух файлов.
    ///
    /// Сбой записи (нет прав, диск занят, файл заблокирован) не пробрасывается:
    /// журнал не должен прерывать работу с проектом. Такой сбой попадает
    /// в <see cref="Debug"/>-вывод и молча игнорируется.
    /// </summary>
    public sealed class FileAppLogger : IAppLogger
    {
        /// <summary>Порог ротации: 1 МБ.</summary>
        public const long MaxFileSizeBytes = 1024 * 1024;

        private const string LogFileName = "gcodegenerator.log";
        private const string ArchiveFileName = "gcodegenerator.1.log";

        private readonly object _sync = new object();
        private readonly string _directory;
        private readonly string _filePath;
        private readonly string _archivePath;

        /// <summary>
        /// Журнал в каталоге приложения внутри <c>%LOCALAPPDATA%</c>.
        /// </summary>
        public FileAppLogger()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GCodeGenerator",
                "logs"))
        {
        }

        /// <summary>
        /// Журнал в указанном каталоге (используется тестами).
        /// </summary>
        /// <param name="directory">Каталог файлов журнала.</param>
        public FileAppLogger(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Каталог журнала не задан.", nameof(directory));

            _directory = directory;
            _filePath = Path.Combine(_directory, LogFileName);
            _archivePath = Path.Combine(_directory, ArchiveFileName);
        }

        /// <summary>Полный путь к текущему файлу журнала.</summary>
        public string FilePath => _filePath;

        /// <inheritdoc />
        public void Log(LogLevel level, string message, Exception exception = null)
        {
            var line = Format(level, message, exception);
            try
            {
                lock (_sync)
                {
                    Directory.CreateDirectory(_directory);
                    RotateIfNeeded();
                    File.AppendAllText(_filePath, line, Encoding.UTF8);
                }
            }
            catch (Exception loggingFailure) when (
                loggingFailure is IOException
                || loggingFailure is UnauthorizedAccessException
                || loggingFailure is NotSupportedException)
            {
                // Журнал не должен мешать работе: сбой записи виден только в отладке.
                Debug.WriteLine($"[FileAppLogger] Не удалось записать журнал: {loggingFailure.Message}");
            }
        }

        private static string Format(LogLevel level, string message, Exception exception)
        {
            var builder = new StringBuilder();
            builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(level.ToString().ToUpperInvariant());
            builder.Append(' ');
            builder.Append(SingleLine(message));
            builder.AppendLine();
            if (exception != null)
                builder.AppendLine(exception.ToString());
            return builder.ToString();
        }

        /// <summary>
        /// Схлопывает переводы строк: одна запись журнала — одна строка,
        /// иначе многострочное сообщение неотличимо от нескольких записей
        /// (текст исключения пишется отдельным блоком осознанно).
        /// </summary>
        private static string SingleLine(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;
            return message.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        }

        private void RotateIfNeeded()
        {
            var file = new FileInfo(_filePath);
            if (!file.Exists || file.Length < MaxFileSizeBytes)
                return;

            if (File.Exists(_archivePath))
                File.Delete(_archivePath);
            File.Move(_filePath, _archivePath);
        }
    }
}
