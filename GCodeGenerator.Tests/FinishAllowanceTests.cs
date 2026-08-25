using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Припуск на обработку и чистовой проход.
    ///
    /// Припуск по умолчанию был нулевым, а чистовой проход снимает именно
    /// его: стоило поставить галочку — и операция становилась негодной.
    /// Окно при этом её принимало, потому что проверяло два-три поля, а не
    /// операцию целиком, и отказ приходил позже, при генерации программы:
    /// пользователь узнавал об ошибке не там, где её допустил.
    /// </summary>
    [TestClass]
    public class FinishAllowanceTests
    {
        private static PocketCircleOperation Pocket()
            => new PocketCircleOperation { CenterX = 0, CenterY = 0, Radius = 20, TotalDepth = 3, StepDepth = 1 };

        /// <summary>
        /// Новая операция готова к чистовому проходу: припуск задан,
        /// и включение галочки не делает операцию негодной.
        /// </summary>
        [TestMethod]
        public void NewPocket_IsReadyForFinishing()
        {
            var operation = Pocket();

            Assert.IsTrue(operation.FinishAllowance > 0, "У новой операции есть припуск");

            operation.IsFinishingEnabled = true;

            Assert.AreEqual(0, operation.Validate().Count,
                "Включение чистовой обработки не должно делать операцию негодной");
        }

        /// <summary>
        /// Пока ни черновой, ни чистовой проход не включён, припуск в расчёт
        /// не идёт: программа та же, что и с нулевым припуском.
        /// </summary>
        [TestMethod]
        public void WithoutRoughingOrFinishing_AllowanceDoesNotAffectProgram()
        {
            var withAllowance = Pocket();
            var withoutAllowance = Pocket();
            withoutAllowance.FinishAllowance = 0;

            var first = Program(withAllowance);
            var second = Program(withoutAllowance);

            CollectionAssert.AreEqual(first, second, "Припуск не должен менять программу, пока он не запрошен");
        }

        /// <summary>
        /// Обнулённый вручную припуск при включённой чистовой обработке —
        /// по-прежнему ошибка: снимать нечего.
        /// </summary>
        [TestMethod]
        public void FinishingWithZeroAllowance_IsStillRefused()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;
            operation.FinishAllowance = 0;

            var problems = operation.Validate();

            Assert.IsTrue(problems.Any(p => p.Property == nameof(PocketCircleOperation.FinishAllowance)),
                "Проблема должна называть припуск");
        }

        /// <summary>
        /// Окно не принимает параметры, на которых генерация откажется
        /// строить программу, и называет, что именно не так.
        /// </summary>
        [TestMethod]
        public void Dialog_RefusesParametersGenerationWouldReject()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;
            operation.FinishAllowance = 0;

            var dialog = new PocketCircleOperationViewModel(null);
            ((IOperationEditorViewModel)dialog).SetOperation(operation);

            ((ICommand)dialog.OkCommand).Execute(null);

            Assert.IsFalse(dialog.IsAccepted, "Такие параметры окно принимать не должно");
            Assert.IsTrue(dialog.HasValidationError, "И должно об этом сказать");
            StringAssert.Contains(dialog.ValidationSummary, nameof(PocketCircleOperation.FinishAllowance),
                "Названо виновное поле");
        }

        /// <summary>
        /// Верные параметры окно принимает, а перечень проблем очищается:
        /// строгая проверка не должна мешать обычной работе.
        /// </summary>
        [TestMethod]
        public void Dialog_AcceptsValidParameters()
        {
            var operation = Pocket();
            operation.IsFinishingEnabled = true;

            var dialog = new PocketCircleOperationViewModel(null);
            ((IOperationEditorViewModel)dialog).SetOperation(operation);

            ((ICommand)dialog.OkCommand).Execute(null);

            Assert.IsTrue(dialog.IsAccepted, "Заполненные параметры принимаются");
            Assert.IsFalse(dialog.HasValidationError);
            Assert.AreEqual(string.Empty, dialog.ValidationSummary);
        }

        private static string[] Program(PocketCircleOperation operation)
            => new GCodeGenerator.GCodeGenerators.SimpleGCodeGenerator()
                .Generate(
                    new System.Collections.Generic.List<OperationBase> { operation },
                    new GCodeSettings())
                .Lines
                .ToArray();
    }
}
