using System;
using System.Globalization;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Markup;
using GCodeGenerator.Localization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Смена языка в уже открытых окнах.
    ///
    /// Разметка обращается к строкам через <c>{loc:Loc Ключ}</c>. Раньше это
    /// подставляло строку один раз при загрузке окна, поэтому язык нельзя
    /// было сменить, не перезапустив программу. Здесь проверяется, что
    /// надпись перечитывается сама — на настоящей разметке, разобранной
    /// тем же механизмом, что и окна приложения.
    /// </summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public class LiveLocalizationTests
    {
        private const string Markup =
            "<TextBlock xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" " +
            "xmlns:loc=\"clr-namespace:GCodeGenerator.Localization;assembly=GCodeGenerator\" " +
            "Text=\"{loc:Loc MainTitle}\"/>";

        [TestMethod]
        public void ChangingCulture_UpdatesTextAlreadyOnScreen()
        {
            string russian = null;
            string englishAfterSwitch = null;

            RunOnUiThread(() =>
            {
                var manager = new AppLocalizationManager();
                manager.AddAssembly("GCodeGenerator");
                var previous = LocalizationProvider.Instance;
                LocalizationProvider.Instance = manager;

                try
                {
                    manager.ChangeCulture(new CultureInfo("ru"));

                    var text = (TextBlock)XamlReader.Parse(Markup);
                    russian = text.Text;

                    manager.ChangeCulture(new CultureInfo("en"));
                    englishAfterSwitch = text.Text;
                }
                finally
                {
                    LocalizationProvider.Instance = previous;
                }
            });

            Assert.AreEqual("Генератор G-кода", russian);
            Assert.AreEqual("G-code Generator", englishAfterSwitch,
                "Надпись должна перечитаться без пересоздания окна");
        }

        /// <summary>
        /// Разбор разметки выполняется в потоке с однопоточной моделью:
        /// элементы WPF создаются только там.
        /// </summary>
        [SupportedOSPlatform("windows")]
        private static void RunOnUiThread(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
                throw new InvalidOperationException("Не удалось разобрать разметку", failure);
        }
    }
}
