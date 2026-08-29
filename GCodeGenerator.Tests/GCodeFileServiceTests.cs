using System.IO;
using System.Text;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class GCodeFileServiceTests
    {
        /// <summary>
        /// Файл программы начинается с первого кадра, а не с преамбулы
        /// кодировки. Проверяются именно байты на диске: строковое сравнение
        /// BOM не видит, и прежний дефект — EF BB BF перед первым кадром —
        /// жил незамеченным, потому что остальные тесты подменяют сервис
        /// фейком и до файла не доходят.
        /// </summary>
        [TestMethod]
        public void Save_AsciiProgram_WritesExactlyAsciiBytesWithoutBom()
        {
            const string gCode = "G21\r\nG90\r\nM30\r\n";
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nc");
            try
            {
                new GCodeFileService().Save(filePath, gCode);

                var bytes = File.ReadAllBytes(filePath);
                CollectionAssert.AreEqual(
                    Encoding.ASCII.GetBytes(gCode),
                    bytes,
                    "ASCII-программа обязана давать байт-в-байт ASCII-файл: без BOM и без перекодировки.");
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Кодировка — UTF-8, а не ASCII: имя операции пишет пользователь,
        /// и продукт передаёт его как есть. ASCII-кодировка молча превратила
        /// бы такое имя в вопросительные знаки — это та же «тихая подмена»,
        /// которую продукт запрещает себе в G-коде.
        /// </summary>
        [TestMethod]
        public void Save_UserWrittenOperationName_SurvivesRoundTrip()
        {
            const string gCode = "(Фланец задний)\r\nG21\r\nM30\r\n";
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nc");
            try
            {
                new GCodeFileService().Save(filePath, gCode);

                Assert.AreEqual(gCode, File.ReadAllText(filePath, Encoding.UTF8));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Повторное сохранение заменяет назначение только после полной
        /// записи временного файла, а прежняя успешно сохранённая программа
        /// остаётся в .bak. В каталоге не остаются служебные .tmp.
        /// </summary>
        [TestMethod]
        public void Save_OverExistingProgram_AtomicallyReplacesItAndKeepsBackup()
        {
            var directory = TemporaryDirectory();
            var filePath = Path.Combine(directory, "program.nc");
            try
            {
                File.WriteAllText(filePath, "G21\r\nM30\r\n", Encoding.UTF8);

                new GCodeFileService().Save(filePath, "G90\r\nM5\r\nM30\r\n");

                Assert.AreEqual("G90\r\nM5\r\nM30\r\n", File.ReadAllText(filePath, Encoding.UTF8));
                StringAssert.Contains(File.ReadAllText(filePath + ".bak", Encoding.UTF8), "G21");
                Assert.AreEqual(0, Directory.GetFiles(directory, ".program.nc.*.tmp").Length);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// Если атомарная замена невозможна, существующая программа остаётся
        /// байт-в-байт прежней: прямой File.WriteAllText в этом месте уже
        /// успел бы усечь файл до того, как сообщил об ошибке.
        /// </summary>
        [TestMethod]
        public void Save_WhenDestinationCannotBeReplaced_PreservesPreviousProgram()
        {
            var directory = TemporaryDirectory();
            var filePath = Path.Combine(directory, "program.nc");
            const string previous = "G21\r\nG90\r\nM5\r\nM30\r\n";
            try
            {
                File.WriteAllText(filePath, previous, Encoding.UTF8);
                using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.Throws<IOException>(() =>
                        new GCodeFileService().Save(filePath, "G0 X1\r\n"));
                }

                Assert.AreEqual(previous, File.ReadAllText(filePath, Encoding.UTF8));
                Assert.AreEqual(0, Directory.GetFiles(directory, ".program.nc.*.tmp").Length,
                    "неудачная замена очищает только свой временный файл");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private sealed class RecordingGCodeFileService : IGCodeFileService
        {
            public string FilePath { get; private set; }
            public string GCode { get; private set; }

            public void Save(string filePath, string gCode)
            {
                FilePath = filePath;
                GCode = gCode;
            }
        }

        [TestMethod]
        public void MainViewModel_SaveGCode_DelegatesToFileService()
        {
            const string filePath = "virtual-program.nc";
            var fileService = new RecordingGCodeFileService();
            var (main, _, dialog, _) = MainViewModelOperationEditTests.CreateMain(
                gCodeFileService: fileService);
            // Программа живёт строками; текст с переводами строк собирается
            // самим сохранением.
            main.GCodeWorkflow.ProgramLines = new[] { "G0 X1 Y2", "M30" };
            dialog.SaveDialogResult = filePath;

            main.GCodeWorkflow.SaveGCodeCommand.Execute(null);

            Assert.AreEqual(filePath, fileService.FilePath);
            Assert.AreEqual("G0 X1 Y2" + System.Environment.NewLine + "M30" + System.Environment.NewLine, fileService.GCode);
        }

        private static string TemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "gcg-gcode-save-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
