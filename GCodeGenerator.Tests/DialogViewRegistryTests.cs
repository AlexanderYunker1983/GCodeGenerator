using System;
using System.Linq;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Соответствие «view-модель → окно».
    ///
    /// Раньше окно искалось по строке, собранной из пространства имён и
    /// имени класса: переименование пространства имён сборку не ломало —
    /// окно просто переставало находиться, и узнать об этом можно было,
    /// только открыв его. Здесь проверяется, что каждая показываемая
    /// view-модель имеет окно, найденное по настоящим типам.
    /// </summary>
    [TestClass]
    public class DialogViewRegistryTests
    {
        /// <summary>
        /// Диалоги операций открываются по кнопке «изменить»: view-модель
        /// без окна означала бы кнопку, которая ничего не делает.
        /// </summary>
        [TestMethod]
        public void EveryOperationEditor_HasWindow()
        {
            var editors = OperationEditorRegistry.Registrations.Values
                .Concat(OperationEditorRegistry.DrillRegistrations.Values)
                .Distinct();

            foreach (var viewModelType in editors)
            {
                var viewType = DialogViewRegistry.ViewFor(viewModelType);

                Assert.IsNotNull(viewType, viewModelType.Name);
                Assert.AreEqual(viewModelType.Name.Replace("ViewModel", "View"), viewType.Name);
            }
        }

        /// <summary>Окна, открываемые не по операции: настройки и предпросмотр.</summary>
        [TestMethod]
        public void StandaloneDialogs_HaveWindows()
        {
            Assert.AreEqual(typeof(SettingsView), DialogViewRegistry.ViewFor(typeof(SettingsViewModel)));
            Assert.AreEqual(typeof(PreviewView), DialogViewRegistry.ViewFor(typeof(PreviewViewModel)));
        }

        /// <summary>
        /// Учтено каждое окно, у которого есть одноимённая view-модель:
        /// пропуск означал бы окно, которое нельзя открыть.
        /// </summary>
        [TestMethod]
        public void EveryWindowWithViewModel_IsRegistered()
        {
            var assembly = typeof(SettingsView).Assembly;
            var viewModelNames = assembly.GetTypes()
                .Where(type => !type.IsAbstract && type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
                .Select(type => type.Name.Replace("ViewModel", string.Empty))
                .ToHashSet(StringComparer.Ordinal);

            var windowsWithViewModel = assembly.GetTypes()
                .Where(type => !type.IsAbstract
                    && typeof(System.Windows.Window).IsAssignableFrom(type)
                    && type.Name.EndsWith("View", StringComparison.Ordinal))
                .Where(type => viewModelNames.Contains(type.Name.Replace("View", string.Empty)))
                .ToList();

            Assert.IsTrue(windowsWithViewModel.Count > 10, "Окон в приложении должно быть много");
            foreach (var window in windowsWithViewModel)
            {
                Assert.IsTrue(DialogViewRegistry.All.Values.Contains(window),
                    $"{window.Name}: окно не учтено в соответствии");
            }
        }

        /// <summary>
        /// View-модель без окна — ошибка сборки приложения, и сообщение
        /// называет, какого класса не хватает.
        /// </summary>
        [TestMethod]
        public void ViewModelWithoutWindow_IsRefusedWithExpectedName()
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => DialogViewRegistry.ViewFor(typeof(OperationsWorkspaceViewModel)));

            StringAssert.Contains(failure.Message, "OperationsWorkspaceView");
        }

        [TestMethod]
        public void NullViewModelType_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DialogViewRegistry.ViewFor(null));
        }
    }
}
