#nullable enable
using System.Runtime.Versioning;
using System.Windows;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public sealed class MessageServiceOwnershipTests
    {
        [TestMethod]
        public void LoadedMainWindow_IsUsedAsMessageOwner()
        {
            TestApplication.Run(() =>
            {
                var application = Application.Current;
                Assert.IsNotNull(application);
                var previousMainWindow = application.MainWindow;
                var window = new Window
                {
                    ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -10000,
                    Top = -10000,
                };

                try
                {
                    application.MainWindow = window;
                    window.Show();

                    Assert.AreSame(window, WpfMessageService.FindOwner(),
                        "Модальное сообщение блокирует главное окно приложения");
                }
                finally
                {
                    window.Close();
                    application.MainWindow = previousMainWindow;
                }
            });
        }

        [TestMethod]
        public void UnloadedMainWindow_IsNotUsedAsMessageOwner()
        {
            TestApplication.Run(() =>
            {
                var application = Application.Current;
                Assert.IsNotNull(application);
                var previousMainWindow = application.MainWindow;
                var window = new Window();

                try
                {
                    application.MainWindow = window;

                    Assert.IsNull(WpfMessageService.FindOwner(),
                        "До загрузки окна сообщение показывается без недействительного владельца");
                }
                finally
                {
                    window.Close();
                    application.MainWindow = previousMainWindow;
                }
            });
        }
    }
}
