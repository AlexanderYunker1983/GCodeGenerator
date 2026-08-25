using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Tests.Fixtures;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// До нижних полей окна можно добраться на невысоком экране.
    ///
    /// Окно настроек не помещалось по высоте: последняя группа параметров
    /// обрезалась, окно не растягивалось и не прокручивалось, поэтому
    /// добраться до конечной точки программы было нельзя вовсе. Здесь
    /// проверяется общее правило: содержимое каждого окна либо помещается,
    /// либо прокручивается.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class WindowContentReachableTests
    {
        /// <summary>Заведомо низкий экран: содержимому придётся прокручиваться.</summary>
        private static readonly Size SmallScreen = new Size(700, 300);

        /// <summary>
        /// Окно, которое нельзя растянуть, обязано прокручиваться: иначе до
        /// содержимого, не поместившегося по высоте, не добраться никак.
        /// Именно так и было с окном настроек.
        /// </summary>
        [TestMethod]
        public void FixedSizeWindow_MustBeScrollable()
        {
            var problems = new List<string>();
            var checkedWindows = 0;

            TestApplication.Run(() =>
            {
                foreach (var windowType in WindowTypes())
                {
                    var window = (Window)Activator.CreateInstance(windowType);
                    try
                    {
                        checkedWindows++;
                        if (window.ResizeMode != ResizeMode.NoResize)
                            continue;

                        LayoutOnSmallScreen(window);

                        var scrollable = Descendants(window).OfType<ScrollViewer>()
                            .Any(viewer => viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled);

                        if (!scrollable)
                        {
                            problems.Add(
                                $"{windowType.Name}: окно нельзя ни растянуть, ни прокрутить");
                        }
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            });

            Assert.IsTrue(checkedWindows > 0, "Ни одного окна не проверено");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        /// <summary>
        /// Окно настроек на низком экране: его можно растянуть, а до нижних
        /// полей добраться прокруткой. Прежде нельзя было ни того, ни
        /// другого — последняя группа параметров просто обрезалась.
        ///
        /// Обе проверки живут в одном методе: приложение WPF существует в
        /// единственном экземпляре на весь прогон, и раскладка окон в разных
        /// потоках даёт разные результаты.
        /// </summary>
        [TestMethod]
        public void SettingsWindow_IsReachableOnSmallScreen()
        {
            var resizeMode = ResizeMode.NoResize;
            double scrollable = 0;

            TestApplication.Run(() =>
            {
                var window = new GCodeGenerator.Views.SettingsView();
                try
                {
                    resizeMode = window.ResizeMode;
                    LayoutOnSmallScreen(window);

                    scrollable = Descendants(window).OfType<ScrollViewer>()
                        .Select(viewer => viewer.ScrollableHeight)
                        .DefaultIfEmpty(0)
                        .Max();
                }
                finally
                {
                    window.Close();
                }
            });

            Assert.AreNotEqual(ResizeMode.NoResize, resizeMode, "Окно настроек должно растягиваться");
            Assert.IsTrue(scrollable > 0,
                "На низком экране окно настроек должно прокручиваться, иначе нижние поля недостижимы");
        }

        /// <summary>
        /// Раскладывает окно так, будто экран невысок. Размер задаётся самому
        /// окну: разметка называет свою высоту, и без этого окно измерялось бы
        /// ею, а не размером экрана.
        /// </summary>
        /// <param name="window">Окно приложения.</param>
        private static void LayoutOnSmallScreen(Window window)
        {
            window.Width = SmallScreen.Width;
            window.Height = SmallScreen.Height;

            // Окно показывается за краем экрана: полная раскладка — с
            // применением шаблонов вкладок и вычислением прокрутки —
            // происходит только у показанного окна.
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.ShowInTaskbar = false;
            window.Left = -10000;
            window.Top = -10000;
            window.Show();
            window.UpdateLayout();

            // Раскладка вкладок завершается в очереди диспетчера: без этого
            // прокрутка ещё не вычислена, и окно выглядит помещающимся.
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }

        private static IEnumerable<Type> WindowTypes()
            => typeof(MainViewModel).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(Window).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name, StringComparer.Ordinal);
    }
}
