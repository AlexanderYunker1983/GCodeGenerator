using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Файловый журнал приложения: формат записи, ротация по размеру,
    /// устойчивость к сбою записи и потокобезопасность.
    /// </summary>
    [TestClass]
    public class FileAppLoggerTests
    {
        private string _directory;

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GCodeGeneratorLoggerTests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Каталог занят другим процессом — тест это не проверяет.
            }
        }

        [TestMethod]
        public void Log_WritesLevelAndMessage()
        {
            var logger = new FileAppLogger(_directory);

            logger.Info("проект открыт");
            logger.Warning("ключ не найден");

            var text = File.ReadAllText(logger.FilePath, Encoding.UTF8);
            StringAssert.Contains(text, "INFO проект открыт");
            StringAssert.Contains(text, "WARNING ключ не найден");
        }

        [TestMethod]
        public void Log_Exception_WritesStackTrace()
        {
            var logger = new FileAppLogger(_directory);

            Exception captured;
            try
            {
                throw new InvalidOperationException("тестовый сбой");
            }
            catch (InvalidOperationException ex)
            {
                captured = ex;
            }

            logger.Error("генерация не удалась", captured);

            var text = File.ReadAllText(logger.FilePath, Encoding.UTF8);
            StringAssert.Contains(text, "ERROR генерация не удалась");
            StringAssert.Contains(text, "InvalidOperationException");
            StringAssert.Contains(text, "тестовый сбой");
        }

        /// <summary>
        /// Многострочное сообщение схлопывается в одну строку, иначе запись
        /// журнала неотличима от нескольких записей.
        /// </summary>
        [TestMethod]
        public void Log_MultilineMessage_CollapsedToSingleLine()
        {
            var logger = new FileAppLogger(_directory);

            logger.Info("первая\nвторая\r\nтретья");

            var lines = File.ReadAllLines(logger.FilePath, Encoding.UTF8);
            Assert.AreEqual(1, lines.Length, "Одно сообщение — одна строка журнала");
            StringAssert.Contains(lines[0], "первая вторая третья");
        }

        /// <summary>
        /// Превышение порога переносит журнал в архив, а новые записи попадают
        /// в пустой текущий файл: журнал занимает не более двух файлов.
        /// </summary>
        [TestMethod]
        public void Log_ExceedingSizeLimit_RotatesToArchive()
        {
            var logger = new FileAppLogger(_directory);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(logger.FilePath, new string('x', (int)FileAppLogger.MaxFileSizeBytes + 1));

            logger.Info("после ротации");

            var archivePath = Path.Combine(_directory, "gcodegenerator.1.log");
            Assert.IsTrue(File.Exists(archivePath), "Прежний файл должен уйти в архив");
            var current = File.ReadAllText(logger.FilePath, Encoding.UTF8);
            StringAssert.Contains(current, "после ротации");
            Assert.IsFalse(current.Contains("xxxx"), "Текущий файл начинается заново");
            Assert.AreEqual(2, Directory.GetFiles(_directory).Length, "Не больше двух файлов журнала");
        }

        /// <summary>
        /// Вторая ротация вытесняет предыдущий архив, а не накапливает файлы.
        /// </summary>
        [TestMethod]
        public void Log_SecondRotation_ReplacesPreviousArchive()
        {
            var logger = new FileAppLogger(_directory);
            Directory.CreateDirectory(_directory);
            var archivePath = Path.Combine(_directory, "gcodegenerator.1.log");
            File.WriteAllText(archivePath, "старый архив");
            File.WriteAllText(logger.FilePath, new string('x', (int)FileAppLogger.MaxFileSizeBytes + 1));

            logger.Info("после второй ротации");

            Assert.AreEqual(2, Directory.GetFiles(_directory).Length);
            Assert.IsFalse(File.ReadAllText(archivePath, Encoding.UTF8).Contains("старый архив"));
        }

        /// <summary>
        /// Сбой записи (путь занят файлом вместо каталога) не должен прерывать
        /// работу приложения: журнал — вспомогательная служба.
        /// </summary>
        [TestMethod]
        public void Log_UnwritableDirectory_DoesNotThrow()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_directory));
            File.WriteAllText(_directory, "это файл, а не каталог");

            var logger = new FileAppLogger(_directory);

            logger.Error("сбой записи не должен всплывать", new InvalidOperationException());

            File.Delete(_directory);
        }

        [TestMethod]
        public void Log_ConcurrentWrites_KeepEveryRecord()
        {
            var logger = new FileAppLogger(_directory);

            Parallel.For(0, 200, i => logger.Info($"запись {i}"));

            var lines = File.ReadAllLines(logger.FilePath, Encoding.UTF8);
            Assert.AreEqual(200, lines.Length, "Все записи должны сохраниться без потерь и склеек");
        }

        [TestMethod]
        public void NullLogger_DoesNothing()
        {
            NullAppLogger.Instance.Error("сообщение", new InvalidOperationException());
            NullAppLogger.Instance.Info("сообщение");
            Assert.IsFalse(Directory.Exists(_directory));
        }
    }
}
