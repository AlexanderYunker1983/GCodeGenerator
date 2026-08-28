#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Проект открывается по пути к файлу, а не только через окно выбора.
    ///
    /// Формат .ygc — собственный формат продукта, и открыть его больше нечем.
    /// Прежде путь мог прийти единственным способом — из окна выбора файла:
    /// аргументы командной строки не читались вовсе, поэтому двойной щелчок
    /// по проекту в проводнике не открывал его, а перетаскивание файла в окно
    /// ничего не делало.
    /// </summary>
    [TestClass]
    public class OpenProjectByPathTests
    {
        // ------------------------------------------------------------------
        // Открытие по пути
        // ------------------------------------------------------------------

        /// <summary>
        /// Полный путь: настоящий файл проекта открывается по пути, минуя окно
        /// выбора, и его операции оказываются в документе.
        /// </summary>
        [TestMethod]
        public async Task OpenByPath_LoadsTheProject_WithoutAskingForAFile()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            var file = WriteProject(new PocketCircleOperation { Name = "Bearing seat" });
            try
            {
                var opened = await main.OpenProjectAsync(file);

                Assert.IsTrue(opened, dialogs.LastErrorMessage ?? "Проект открыт");
                Assert.AreEqual(0, dialogs.OpenDialogCount, "Окно выбора файла не показывалось");
                Assert.AreEqual(file, main.ProjectWorkflow.CurrentPath, "Файл стал текущим");
                Assert.AreEqual(1, main.OperationsWorkspace.AllOperations.Count, "Операция из файла");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Несуществующий файл — сообщение об ошибке, а не тихий отказ:
        /// путь мог прийти из ярлыка на удалённый проект.
        /// </summary>
        [TestMethod]
        public async Task OpenByPath_MissingFile_ReportsTheError()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();

            var opened = await main.OpenProjectAsync(
                Path.Combine(Path.GetTempPath(), "no-such-project.ygc"));

            Assert.IsFalse(opened);
            Assert.AreEqual(1, dialogs.ErrorMessageCount, "Об отказе сказано");
        }

        /// <summary>Настоящий файл проекта с одной операцией.</summary>
        private static string WriteProject(OperationBase operation)
        {
            var file = Path.Combine(
                Path.GetTempPath(), "gcg_open_" + Guid.NewGuid().ToString("N") + ".ygc");
            new Persistence.ProjectFileService().Save(file, new[] { operation }, new GCodeSettings());
            return file;
        }

        /// <summary>
        /// Пустой путь — не ошибка, а отсутствие файла: так выглядит запуск
        /// программы без аргументов.
        /// </summary>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        public async Task OpenByPath_WithoutPath_DoesNothing(string? path)
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();

            var opened = await main.OpenProjectAsync(path);

            Assert.IsFalse(opened);
            Assert.AreEqual(0, dialogs.ErrorMessageCount, "Сообщения об ошибке нет");
            Assert.IsNull(main.ProjectWorkflow.CurrentPath);
        }

        /// <summary>
        /// Открытие по пути спрашивает о несохранённых изменениях так же,
        /// как открытие через окно выбора: файл, брошенный в окно, не должен
        /// молча стирать работу.
        /// </summary>
        [TestMethod]
        public async Task OpenByPath_WithUnsavedChanges_AsksAndCanBeCancelled()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(new PocketCircleOperation());
            dialogs.SaveConfirmationResult = SaveConfirmation.Cancel;

            var opened = await main.OpenProjectAsync("project.ygc");

            Assert.IsFalse(opened, "Отказ от продолжения отменяет открытие");
            Assert.AreEqual(1, dialogs.SaveConfirmationCount, "Вопрос задан");
            Assert.IsNull(main.ProjectWorkflow.CurrentPath, "Прежний документ на месте");
        }

        // ------------------------------------------------------------------
        // Путь из командной строки
        // ------------------------------------------------------------------

        /// <summary>
        /// Оболочка запускает программу с путём к файлу, по которому щёлкнули.
        /// Берётся существующий файл: аргумент может оказаться ключом запуска
        /// или путём, которого уже нет.
        /// </summary>
        [TestMethod]
        public void CommandLine_FindsAnExistingFile()
        {
            var file = Path.Combine(Path.GetTempPath(), "gcg_open_" + Guid.NewGuid().ToString("N") + ".ygc");
            File.WriteAllText(file, "{}");
            try
            {
                Assert.AreEqual(file, App.ProjectFileFromCommandLine(new[] { file }));
                Assert.AreEqual(file, App.ProjectFileFromCommandLine(new[] { "--debug", file }));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void CommandLine_WithoutAnExistingFile_FindsNothing()
        {
            Assert.IsNull(App.ProjectFileFromCommandLine(Array.Empty<string>()));
            Assert.IsNull(App.ProjectFileFromCommandLine(new[] { "--debug" }));
            Assert.IsNull(App.ProjectFileFromCommandLine(new[] { "" }));
            Assert.IsNull(App.ProjectFileFromCommandLine(
                new[] { Path.Combine(Path.GetTempPath(), "no-such-project.ygc") }));
        }

        // ------------------------------------------------------------------
        // Файл, перетащенный в окно
        // ------------------------------------------------------------------

        [TestMethod]
        public void DroppedProject_IsAccepted()
        {
            Assert.AreEqual(
                @"C:\work\part.ygc",
                MainView.ProjectFileFrom(FileDrop(@"C:\work\part.ygc")));
        }

        /// <summary>Расширение сверяется без учёта регистра: проводник отдаёт путь как есть.</summary>
        [TestMethod]
        public void DroppedProject_WithUpperCaseExtension_IsAccepted()
        {
            Assert.AreEqual(
                @"C:\work\PART.YGC",
                MainView.ProjectFileFrom(FileDrop(@"C:\work\PART.YGC")));
        }

        /// <summary>
        /// Из нескольких файлов берётся первый проект: открыть можно только
        /// один, и выбрать «какой-то» из набора хуже, чем назвать первый.
        /// </summary>
        [TestMethod]
        public void DroppedSet_TakesTheFirstProject()
        {
            Assert.AreEqual(
                @"C:\work\second.ygc",
                MainView.ProjectFileFrom(FileDrop(
                    @"C:\work\drawing.dxf", @"C:\work\second.ygc", @"C:\work\third.ygc")));
        }

        [TestMethod]
        public void DroppedNonProject_IsRefused()
        {
            Assert.IsNull(MainView.ProjectFileFrom(FileDrop(@"C:\work\drawing.dxf")));
            Assert.IsNull(MainView.ProjectFileFrom(FileDrop(@"C:\work\program.nc")));
            Assert.IsNull(MainView.ProjectFileFrom(new DataObject(DataFormats.Text, "просто текст")));
            Assert.IsNull(MainView.ProjectFileFrom(null));
        }

        /// <summary>Перетаскиваемый набор файлов — так его отдаёт проводник.</summary>
        private static IDataObject FileDrop(params string[] files)
            => new DataObject(DataFormats.FileDrop, files);
    }
}
