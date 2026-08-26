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
            main.GCodeWorkflow.ProgramLines = new[] { "stale G-code" };
            main.OperationsWorkspace.AllOperations.Add(new ProfileCircleOperation { Radius = 0 });

            await ((IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);

            Assert.IsFalse(main.GCodeWorkflow.IsGenerating);
            Assert.AreEqual(0, main.GCodeWorkflow.ProgressPercent);
            Assert.AreEqual(string.Empty, main.GCodeWorkflow.GCodePreview, "Устаревшая программа убрана");
            Assert.IsFalse(string.IsNullOrEmpty(dialogService.LastErrorMessage), "Причина показана");
            StringAssert.Contains(dialogService.LastErrorMessage, "Radius", "Назван параметр");
        }

        /// <summary>
        /// Пустая операция в списке — например, из файла проекта, написанного
        /// вручную, — доходит до проверки перед генерацией и отклоняется с
        /// показом причины. Прежде окно молча выбрасывало пустоту из слепка,
        /// и проверка ядра, спроектированная её отклонять, не получала шанса.
        /// </summary>
        [TestMethod]
        public async Task NullOperation_ReachesValidationAndIsReported()
        {
            var (main, _, dialogService, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(new ProfileCircleOperation { Radius = 10 });
            main.OperationsWorkspace.AllOperations.Add(null);

            await ((IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);

            Assert.AreEqual(string.Empty, main.GCodeWorkflow.GCodePreview, "Программа не построена");
            Assert.IsFalse(string.IsNullOrEmpty(dialogService.LastErrorMessage), "Причина показана");
        }
    }
}
