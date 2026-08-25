using System.Collections.ObjectModel;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Семантика OK и отмены в диалоге операции.
    ///
    /// Окно правит рабочую копию операции напрямую, поэтому OK ничего не
    /// переносит — он только проверяет параметры и закрывает окно. Отмена
    /// закрывает окно, а копия с правками выбрасывается.
    /// </summary>
    [TestClass]
    public class OperationEditorViewModelBaseTests
    {
        /// <summary>Фейковая диалоговая VM поверх базового класса.</summary>
        private sealed class FakeEditorVm : OperationEditorViewModelBase<PocketCircleOperation>
        {
            public bool Valid { get; set; } = true;

            public int AcceptedCount { get; private set; }

            protected override bool IsValid() => Valid;

            protected override void BeforeAccept(PocketCircleOperation operation) => AcceptedCount++;
        }

        private static (FakeEditorVm vm, PocketCircleOperation op, ObservableCollection<OperationBase> ops, int[] closeCount) Create()
        {
            var ops = new ObservableCollection<OperationBase>();
            var op = new PocketCircleOperation { Radius = 10 };
            ops.Add(op);
            var vm = new FakeEditorVm();
            var closeCount = new[] { 0 };
            vm.CloseRequested += () => closeCount[0]++;
            vm.Operation = op;
            return (vm, op, ops, closeCount);
        }

        [TestMethod]
        public void Operation_IsEditedDirectly()
        {
            var (vm, op, _, _) = Create();

            vm.Operation.Radius = 20;

            Assert.AreSame(op, vm.Operation, "Диалог правит ту операцию, которую ему дали");
            Assert.AreEqual(20.0, op.Radius, 1e-9, "Правка сразу в операции — переносить нечего");
        }

        [TestMethod]
        public void Ok_Valid_AcceptsAndCloses()
        {
            var (vm, _, ops, closeCount) = Create();
            vm.Operation.Radius = 20;

            vm.OkCommand.Execute(null);

            Assert.IsTrue(vm.IsAccepted, "OK принимает параметры");
            Assert.AreEqual(1, vm.AcceptedCount, "Перед принятием окно получает последнее слово");
            Assert.AreEqual(1, ops.Count, "Операция остаётся в коллекции");
            Assert.AreEqual(1, closeCount[0], "OK закрывает окно");
        }

        /// <summary>
        /// Неверные параметры — повод их исправить, а не потерять операцию:
        /// окно остаётся открытым с пояснением.
        /// </summary>
        [TestMethod]
        public void Ok_Invalid_KeepsDialogOpen()
        {
            var (vm, _, ops, closeCount) = Create();
            vm.Valid = false;

            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.IsAccepted, "Параметры не приняты");
            Assert.AreEqual(0, vm.AcceptedCount, "Принятие не выполнялось");
            Assert.AreEqual(1, ops.Count, "Операция остаётся в коллекции");
            Assert.AreEqual(0, closeCount[0], "Окно не закрывается");
            Assert.IsTrue(vm.HasValidationError, "Окно показывает, что параметры неверны");
        }

        [TestMethod]
        public void Ok_AfterFixingParameters_AcceptsAndCloses()
        {
            var (vm, _, _, closeCount) = Create();
            vm.Valid = false;
            vm.OkCommand.Execute(null);

            vm.Valid = true;
            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.HasValidationError, "Пояснение об ошибке снято");
            Assert.IsTrue(vm.IsAccepted, "Исправленные параметры приняты");
            Assert.AreEqual(1, closeCount[0], "Окно закрывается только после исправления");
        }

        [TestMethod]
        public void Cancel_Closes_WithoutAccepting()
        {
            var (vm, _, ops, closeCount) = Create();
            vm.Operation.Radius = 999;

            vm.CancelCommand.Execute(null);

            Assert.IsFalse(vm.IsAccepted, "Отмена не принимает параметры");
            Assert.AreEqual(0, vm.AcceptedCount, "Принятие не выполнялось");
            Assert.AreEqual(1, ops.Count, "Операция не удалена");
            Assert.AreEqual(1, closeCount[0], "Cancel закрывает окно");
        }

        [TestMethod]
        public void OnClosed_DoesNothing()
        {
            var (vm, _, ops, closeCount) = Create();

            vm.OnClosed();

            Assert.IsFalse(vm.IsAccepted, "Закрытие окна само по себе ничего не принимает");
            Assert.AreEqual(1, ops.Count, "Операция не удалена");
            Assert.AreEqual(0, closeCount[0], "OnClosed не запрашивает закрытие");
        }

        [TestMethod]
        public void Ok_NullOperation_DoesNothing()
        {
            var vm = new FakeEditorVm();

            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.IsAccepted, "OK без операции — нет-оп");
        }
    }
}
