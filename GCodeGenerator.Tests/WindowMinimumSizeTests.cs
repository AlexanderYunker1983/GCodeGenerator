using System.IO;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Главное окно помещается на экран.
    ///
    /// Наименьший размер задан в единицах, не зависящих от устройства, и
    /// сравнивать его надо не с числом точек экрана, а с рабочей областью
    /// в тех же единицах. На распространённом сочетании «1920×1080 при
    /// масштабе 175 %» её остаётся 1097×617 — прежний минимум 1200×600
    /// по ширине туда не помещался, и окно нельзя было ни уместить,
    /// ни уменьшить.
    ///
    /// Мало объявить размер поменьше: разметка должна в него укладываться.
    /// Поэтому окно здесь измеряется по-настоящему — если содержимое требует
    /// больше объявленного минимума, проверка это назовёт.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class WindowMinimumSizeTests
    {
        /// <summary>
        /// Рабочая область при самых тесных сочетаниях, которые продукт
        /// обещает поддерживать: разрешение экрана в единицах разметки при
        /// заявленном масштабе, за вычетом панели задач.
        /// </summary>
        private static readonly (string Name, double Width, double Height)[] TightScreens =
        {
            ("1920×1080 при 175 %", 1097, 617 - 40),
            ("1920×1080 при 150 %", 1280, 720 - 40),
            ("1366×768 при 100 %", 1366, 768 - 40),
        };

        [TestMethod]
        public void MainWindow_FitsTheTightestSupportedScreen()
        {
            TestApplication.Run(() =>
            {
                var window = new MainView();
                foreach (var (name, width, height) in TightScreens)
                {
                    Assert.IsTrue(window.MinWidth <= width,
                        $"{name}: наименьшая ширина окна {window.MinWidth} не помещается в {width}");
                    Assert.IsTrue(window.MinHeight <= height,
                        $"{name}: наименьшая высота окна {window.MinHeight} не помещается в {height}");
                }

                window.Close();
            });
        }

        /// <summary>
        /// Содержимое укладывается в объявленный минимум: измеренный размер
        /// окна не превышает того, что оно разрешает пользователю выставить.
        /// Иначе минимум был бы обещанием, которого разметка не держит, —
        /// элементы обрезались бы или наезжали друг на друга.
        /// </summary>
        [TestMethod]
        public void MainWindow_ContentFitsItsMinimumSize()
        {
            TestApplication.Run(() =>
            {
                var window = new MainView();
                var available = new Size(window.MinWidth, window.MinHeight);

                window.Measure(available);
                window.Arrange(new Rect(available));
                window.UpdateLayout();

                var content = (FrameworkElement)window.Content;

                Assert.IsTrue(content.DesiredSize.Width <= window.MinWidth + 1,
                    $"Содержимое требует {content.DesiredSize.Width:0} по ширине "
                    + $"при наименьшей ширине окна {window.MinWidth}");
                Assert.IsTrue(content.DesiredSize.Height <= window.MinHeight + 1,
                    $"Содержимое требует {content.DesiredSize.Height:0} по высоте "
                    + $"при наименьшей высоте окна {window.MinHeight}");

                window.Close();
            });
        }

        /// <summary>
        /// Ни одна колонка не схлопывается в ничто при наименьшем размере
        /// окна: прежде при сжатии ужималась одна левая — она забирала
        /// остаток после двух колонок постоянной ширины.
        /// </summary>
        [TestMethod]
        public void MainWindow_KeepsEveryColumnUsable()
        {
            TestApplication.Run(() =>
            {
                var window = new MainView();

                // Окно показывается за краем экрана: без показа шаблон окна
                // не применяется, содержимое не размещается, и фактическая
                // ширина колонок остаётся нулевой — проверять было бы нечего.
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                window.Show();
                window.UpdateLayout();

                var layout = (Grid)window.Content;
                Assert.AreEqual(3, layout.ColumnDefinitions.Count, "Колонок в раскладке");

                foreach (var column in layout.ColumnDefinitions)
                {
                    Assert.IsTrue(column.MinWidth > 0,
                        "У колонки нет наименьшей ширины — при сжатии окна она исчезнет");
                    Assert.IsTrue(column.ActualWidth >= column.MinWidth - 1,
                        $"Колонка сжалась до {column.ActualWidth:0} при наименьшей {column.MinWidth:0}");
                }

                window.Close();
            });
        }

        /// <summary>
        /// Кнопки «о программе» и «настройки» одного размера.
        ///
        /// Значок настроек — картинка 24×24, а знак вопроса сам по себе
        /// занимает высоту строки текста, и кнопка выходила заметно ниже
        /// соседней: две кнопки подряд в одной панели, разной высоты.
        /// </summary>
        [TestMethod]
        public void ToolbarButtons_AreTheSameSize()
        {
            TestApplication.Run(() =>
            {
                var window = new MainView();
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -10000;
                window.Top = -10000;
                window.Show();
                window.UpdateLayout();

                var about = (Button)window.FindName("AboutButton");
                var settings = (Button)window.FindName("SettingsButton");

                Assert.IsNotNull(about, "Кнопка «о программе» переименована");
                Assert.IsNotNull(settings, "Кнопка настроек переименована");

                Assert.AreEqual(settings.ActualWidth, about.ActualWidth, 0.5,
                    $"Ширина: настройки {settings.ActualWidth:0.#}, о программе {about.ActualWidth:0.#}");
                Assert.AreEqual(settings.ActualHeight, about.ActualHeight, 0.5,
                    $"Высота: настройки {settings.ActualHeight:0.#}, о программе {about.ActualHeight:0.#}");
                Assert.IsTrue(about.ActualHeight > 0, "Окно не разместилось — проверять нечего");

                window.Close();
            });
        }

        /// <summary>
        /// Манифест объявляет поддерживаемые версии Windows и осведомлённость
        /// о масштабе экрана. Без него система считает программу написанной
        /// до Vista и включает режим совместимости.
        /// </summary>
        [TestMethod]
        public void Application_DeclaresItsManifest()
        {
            var root = RepositoryRootLocator.Find();
            var manifest = File.ReadAllText(Path.Combine(root, "GCodeGenerator", "app.manifest"));
            var project = File.ReadAllText(Path.Combine(root, "GCodeGenerator", "GCodeGenerator.csproj"));

            StringAssert.Contains(project, "<ApplicationManifest>app.manifest</ApplicationManifest>",
                "Манифест не подключён к сборке");
            StringAssert.Contains(manifest, "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
                "В манифесте не объявлена поддержка Windows 10 и 11");
            StringAssert.Contains(manifest, "PerMonitorV2",
                "В манифесте не объявлена осведомлённость о масштабе экрана");
        }

    }
}
