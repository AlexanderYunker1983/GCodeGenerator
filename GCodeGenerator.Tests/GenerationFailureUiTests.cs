using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Как окно ведёт себя, когда генерация отказалась строить программу.
    ///
    /// Сама проверка параметров живёт в ядре и проверяется его тестами;
    /// здесь — то, что видит пользователь: устаревший текст программы убран,
    /// полоса прогресса сброшена, а причина отказа показана с названием
    /// параметра, а не общей фразой.
    /// </summary>
    [TestClass]
    public class GenerationFailureUiTests
    {
        [TestMethod]
        public async Task ValidationFailure_ClearsPreviewAndShowsReason()
        {
            var (main, _, dialogService, _) = MainViewModelOperationEditTests.CreateMain();
            main.GCodePreview = "stale G-code";
            main.AllOperations.Add(new ProfileCircleOperation { Radius = 0 });

            await ((IAsyncRelayCommand)main.GenerateGCodeCommand).ExecuteAsync(null);

            Assert.IsFalse(main.IsGenerating);
            Assert.AreEqual(0, main.ProgressPercent);
            Assert.AreEqual(string.Empty, main.GCodePreview, "Устаревшая программа убрана");
            Assert.IsFalse(string.IsNullOrEmpty(dialogService.LastErrorMessage), "Причина показана");
            StringAssert.Contains(dialogService.LastErrorMessage, "Radius", "Назван параметр");
        }
    }
}
