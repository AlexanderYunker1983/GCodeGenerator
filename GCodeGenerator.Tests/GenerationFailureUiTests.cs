using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Localization;
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

        /// <summary>
        /// Причина отказа показывается на языке интерфейса. Исключение
        /// проверки строит свой перечень по-английски — он нужен журналу, —
        /// а окну перечень собирается заново, теми же словами, что и в
        /// диалогах операций.
        /// </summary>
        [TestMethod]
        public async Task ValidationFailure_ReasonIsShownInTheInterfaceLanguage()
        {
            var localization = new LocalizationManager();
            localization.AddAssembly("GCodeGenerator");
            localization.ChangeCulture(new CultureInfo("ru"));
            var (main, _, dialogService, _) = MainViewModelOperationEditTests.CreateMain(
                localizationManager: localization);
            main.OperationsWorkspace.AllOperations.Add(
                new ProfileCircleOperation { Name = "Контур", Radius = 0 });

            await ((IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);

            var message = dialogService.LastErrorMessage;
            StringAssert.Contains(message, "Операция №1 «Контур»:", "Операция названа по-русски");
            StringAssert.Contains(message, "Radius: Значение должно быть больше нуля",
                "Причина переведена, параметр назван");
            Assert.IsFalse(message.Contains("must be"), "Английского текста исключения в окне нет");
        }
    }
}
