using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты CloseableViewModel (пункт 7.3 плана): VM запрашивает закрытие
    /// диалогового окна через CloseRequested/RequestClose.
    /// </summary>
    [TestClass]
    public class CloseableViewModelTests
    {
        [TestMethod]
        public void RequestClose_FiresCloseRequested()
        {
            var vm = new CloseableViewModel();
            var called = 0;
            vm.CloseRequested += () => called++;

            vm.RequestClose();

            Assert.AreEqual(1, called, "CloseRequested должен сработать");
        }

        [TestMethod]
        public void RequestClose_NoSubscribers_DoesNotThrow()
        {
            var vm = new CloseableViewModel();

            vm.RequestClose(); // без подписчиков — не бросает

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void RequestClose_MultipleSubscribers_AllNotified()
        {
            var vm = new CloseableViewModel();
            var called = 0;
            vm.CloseRequested += () => called++;
            vm.CloseRequested += () => called++;

            vm.RequestClose();

            Assert.AreEqual(2, called);
        }
    }
}
