#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Сведения о программе и привычные клавиши.
    ///
    /// Окна «О программе» не было вовсе: версию можно было увидеть только
    /// в заголовке главного окна, а путь к журналу работы, лицензию и адрес
    /// страницы продукта пользователю взять было неоткуда — при том, что
    /// журнал просят приложить к сообщению о сбое. Из клавиш работали только
    /// отмена и повтор: сохранение и открытие требовали мыши.
    /// </summary>
    [TestClass]
    public class AboutAndShortcutsTests
    {
        /// <summary>Оболочка в тестах: запоминает, что её просили открыть.</summary>
        private sealed class FakeShell : IShellService
        {
            public List<string?> ShownFiles { get; } = new List<string?>();

            public List<string?> OpenedUrls { get; } = new List<string?>();

            public void ShowFile(string? path) => ShownFiles.Add(path);

            public void OpenUrl(string? url) => OpenedUrls.Add(url);
        }

        private static string MainWindowMarkup => File.ReadAllText(
            Path.Combine(RepositoryRootLocator.Find(), "GCodeGenerator", "Views", "MainView.xaml"));

        // ------------------------------------------------------------------
        // Окно «О программе»
        // ------------------------------------------------------------------

        [TestMethod]
        public void About_ShowsVersionCopyrightAndLogPath()
        {
            var info = new ProgramInfo("1.2.3-rc5", "Copyright (c) 2021-2026 Alexander Yunker", @"C:\logs\app.log");

            var about = new AboutViewModel(null, info, new FakeShell());

            Assert.AreEqual("1.2.3-rc5", about.Version);
            Assert.AreEqual("Copyright (c) 2021-2026 Alexander Yunker", about.Copyright);
            Assert.AreEqual(@"C:\logs\app.log", about.LogFilePath);
        }

        /// <summary>
        /// Журнал показывается в проводнике — его просят приложить к сообщению
        /// о сбое, а искать его в недрах профиля пользователю не с руки.
        /// </summary>
        [TestMethod]
        public void About_ShowsTheLogFileInTheShell()
        {
            var shell = new FakeShell();
            var about = new AboutViewModel(null, new ProgramInfo("1.0", logFilePath: @"C:\logs\app.log"), shell);

            about.ShowLogCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { @"C:\logs\app.log" }, shell.ShownFiles);
        }

        /// <summary>Без пути к журналу показывать нечего — кнопка недоступна.</summary>
        [TestMethod]
        public void About_WithoutLogPath_CannotShowIt()
        {
            var about = new AboutViewModel(null, new ProgramInfo("1.0"), new FakeShell());

            Assert.IsFalse(about.ShowLogCommand.CanExecute(null));
        }

        [TestMethod]
        public void About_OpensTheProductPage()
        {
            var shell = new FakeShell();
            var about = new AboutViewModel(null, new ProgramInfo("1.0"), shell);

            about.OpenRepositoryCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { AboutViewModel.RepositoryUrl }, shell.OpenedUrls);
            StringAssert.StartsWith(AboutViewModel.RepositoryUrl, "https://",
                "Ссылка на страницу продукта должна открываться по защищённому протоколу");
        }

        /// <summary>Окно закрывается своей кнопкой, как остальные диалоги.</summary>
        [TestMethod]
        public void About_ClosesOnRequest()
        {
            var about = new AboutViewModel(null, new ProgramInfo("1.0"), new FakeShell());
            var closed = 0;
            about.CloseRequested += () => closed++;

            about.CloseCommand.Execute(null);

            Assert.AreEqual(1, closed);
        }

        /// <summary>
        /// Программа умеет показать окно: команда есть у главной view-модели,
        /// а окно для неё находится по имени в реестре диалогов.
        /// </summary>
        [TestMethod]
        public void About_HasAWindowOfItsOwn()
        {
            Assert.AreEqual(
                typeof(Views.AboutView),
                Views.DialogViewRegistry.ViewFor(typeof(AboutViewModel)));
        }

        // ------------------------------------------------------------------
        // Клавиши и подсказки
        // ------------------------------------------------------------------

        /// <summary>
        /// Привычные клавиши работы с документом объявлены в главном окне.
        /// Человек нажимает Ctrl+S не задумываясь, и потерять эту привязку
        /// правкой разметки легко — сборка о ней ничего не знает.
        /// </summary>
        [TestMethod]
        [DataRow("Control", "N", "NewProgramCommand")]
        [DataRow("Control", "O", "OpenProjectCommand")]
        [DataRow("Control", "S", "SaveProjectCommand")]
        [DataRow("Control+Shift", "S", "SaveProjectAsCommand")]
        [DataRow("Control", "Z", "UndoCommand")]
        [DataRow("Control", "Y", "RedoCommand")]
        [DataRow("Control", "G", "GenerateGCodeCommand")]
        public void MainWindow_BindsTheUsualShortcut(string modifiers, string key, string command)
        {
            var expected = new Regex(
                @"<KeyBinding\s+Modifiers=""" + Regex.Escape(modifiers)
                + @"""\s+Key=""" + key + @"""\s+Command=""\{Binding [^}]*" + command + @"\}""");

            Assert.IsTrue(expected.IsMatch(MainWindowMarkup),
                $"MainView.xaml: нет привязки {modifiers}+{key} к {command}");
        }

        [TestMethod]
        public void MainWindow_OpensAboutWithF1()
        {
            StringAssert.Contains(
                MainWindowMarkup,
                @"<KeyBinding Key=""F1"" Command=""{Binding OpenAboutCommand}""/>",
                "MainView.xaml: F1 не открывает окно «О программе»");
        }

        /// <summary>
        /// У каждой кнопки панели есть подсказка: кнопки создания и открытия
        /// не подписаны вовсе, а понять их по одной иконке можно не всегда.
        /// </summary>
        [TestMethod]
        public void EveryToolbarButton_HasATooltip()
        {
            var buttons = Regex.Matches(MainWindowMarkup, @"<Button\b[^>]*?(?:/>|>)", RegexOptions.Singleline)
                .Select(match => match.Value)
                .Where(button => button.Contains("ProjectWorkflow.") || button.Contains("OpenSettingsCommand")
                                 || button.Contains("OpenAboutCommand"))
                .ToList();

            Assert.IsTrue(buttons.Count >= 6, $"Кнопок панели найдено {buttons.Count}");
            foreach (var button in buttons)
            {
                StringAssert.Contains(button, "ToolTip=",
                    "Кнопка панели без подсказки: " + Regex.Match(button, @"Command=""[^""]*""").Value);
            }
        }

        /// <summary>
        /// Подсказки называют клавишу: иначе о ней узнают, только прочитав
        /// документацию, а до неё ещё нужно дойти.
        /// </summary>
        [TestMethod]
        [DataRow("NewProjectToolTip", "Ctrl+N")]
        [DataRow("OpenProjectToolTip", "Ctrl+O")]
        [DataRow("SaveProjectToolTip", "Ctrl+S")]
        [DataRow("SaveProjectAsToolTip", "Ctrl+Shift+S")]
        [DataRow("AboutToolTip", "F1")]
        public void Tooltip_NamesItsShortcut(string key, string shortcut)
        {
            foreach (var culture in new[] { "", ".ru" })
            {
                var resources = File.ReadAllText(Path.Combine(
                    RepositoryRootLocator.Find(), "GCodeGenerator", "Resources",
                    $"LocalizableResources{culture}.resx"));

                var value = Regex.Match(
                    resources,
                    @"<data name=""" + key + @"""[^>]*>\s*<value>(?<text>[^<]*)</value>");

                Assert.IsTrue(value.Success, $"Нет подсказки {key} в наборе «{culture}»");
                StringAssert.Contains(value.Groups["text"].Value, shortcut,
                    $"Подсказка {key} не называет клавишу");
            }
        }
    }
}
