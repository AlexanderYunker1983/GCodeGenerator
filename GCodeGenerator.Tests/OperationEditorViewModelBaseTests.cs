using System.Collections.ObjectModel;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты OperationEditorViewModelBase (пункт 7.3 плана): явная семантика
    /// OK/Cancel — OK сохраняет (или удаляет невалидную) и закрывает, Cancel и
    /// OnClosed — без изменений.
    /// </summary>
    [TestClass]
    public class OperationEditorViewModelBaseTests
    {
        /// <summary>Фейковая диалоговая VM поверх базового класса.</summary>
        private sealed class FakeEditorVm : OperationEditorViewModelBase<PocketCircleOperation>
        {
            public bool ApplyCalled { get; private set; }
            public double Radius { get; set; }
            public bool Valid { get; set; } = true;

            protected override void LoadFromOperation(PocketCircleOperation operation)
            {
                Radius = operation.Radius;
            }

            protected override void ApplyToOperation()
            {
                ApplyCalled = true;
                Operation.Radius = Radius;
            }

            protected override bool IsValid() => Valid;
        }

        private static (FakeEditorVm vm, PocketCircleOperation op, ObservableCollection<OperationBase> ops, int[] closeCount) Create()
        {
            var ops = new ObservableCollection<OperationBase>();
            var op = new PocketCircleOperation { Radius = 10 };
            ops.Add(op);
            var vm = new FakeEditorVm { Operations = ops };
            var closeCount = new[] { 0 };
            vm.CloseRequested += () => closeCount[0]++;
            vm.Operation = op;
            return (vm, op, ops, closeCount);
        }

        [TestMethod]
        public void OperationSetter_LoadsTypedProperties()
        {
            var (vm, op, _, _) = Create();
            Assert.AreEqual(10.0, vm.Radius, 1e-9, "сеттер Operation читает значения в свойства VM");
            Assert.AreSame(op, vm.Operation);
        }

        [TestMethod]
        public void Ok_Valid_SavesAndCloses()
        {
            var (vm, op, ops, closeCount) = Create();
            vm.Radius = 20;

            vm.OkCommand.Execute(null);

            Assert.IsTrue(vm.ApplyCalled, "OK вызывает ApplyToOperation");
            Assert.AreEqual(20.0, op.Radius, 1e-9, "значения VM сохранены в операцию");
            Assert.AreEqual(1, ops.Count, "валидная операция не удаляется");
            Assert.AreEqual(1, closeCount[0], "OK закрывает окно");
        }

        /// <summary>
        /// Неверные параметры — повод их исправить, а не потерять операцию:
        /// окно остаётся открытым с пояснением, операция не трогается.
        /// </summary>
        [TestMethod]
        public void Ok_Invalid_KeepsDialogOpenAndOperationIntact()
        {
            var (vm, op, ops, closeCount) = Create();
            vm.Radius = 999;
            vm.Valid = false;

            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.ApplyCalled, "невалидные значения не сохраняются");
            Assert.AreEqual(10.0, op.Radius, 1e-9, "операция не изменена");
            Assert.AreEqual(1, ops.Count, "операция остаётся в коллекции");
            Assert.AreEqual(0, closeCount[0], "окно не закрывается");
            Assert.IsTrue(vm.HasValidationError, "окно показывает, что параметры неверны");
            Assert.IsFalse(vm.IsAccepted, "изменения не приняты");
        }

        /// <summary>
        /// Исправленные параметры сохраняются обычным образом, а пояснение
        /// об ошибке исчезает.
        /// </summary>
        [TestMethod]
        public void Ok_AfterFixingParameters_SavesAndCloses()
        {
            var (vm, op, ops, closeCount) = Create();
            vm.Valid = false;
            vm.OkCommand.Execute(null);

            vm.Valid = true;
            vm.Radius = 20;
            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.HasValidationError, "пояснение об ошибке снято");
            Assert.AreEqual(20.0, op.Radius, 1e-9, "исправленные значения сохранены");
            Assert.AreEqual(1, ops.Count, "операция на месте");
            Assert.AreEqual(1, closeCount[0], "окно закрывается только после исправления");
        }

        [TestMethod]
        public void Cancel_NoChanges_Closes()
        {
            var (vm, op, ops, closeCount) = Create();
            vm.Radius = 999;

            vm.CancelCommand.Execute(null);

            Assert.IsFalse(vm.ApplyCalled, "Cancel не сохраняет");
            Assert.AreEqual(10.0, op.Radius, 1e-9, "операция не изменена");
            Assert.AreEqual(1, ops.Count, "операция не удалена");
            Assert.AreEqual(1, closeCount[0], "Cancel закрывает окно");
        }

        [TestMethod]
        public void OnClosed_NoSave_NoRemove()
        {
            var (vm, op, ops, closeCount) = Create();
            vm.Radius = 999;

            vm.OnClosed();

            Assert.IsFalse(vm.ApplyCalled, "OnClosed больше не сохраняет (пункт 7.3)");
            Assert.AreEqual(10.0, op.Radius, 1e-9, "операция не изменена");
            Assert.AreEqual(1, ops.Count, "операция не удалена");
            Assert.AreEqual(0, closeCount[0], "OnClosed не запрашивает закрытие");
        }

        [TestMethod]
        public void Ok_NullOperation_DoesNothing()
        {
            var vm = new FakeEditorVm { Operations = new ObservableCollection<OperationBase>() };

            vm.OkCommand.Execute(null);

            Assert.IsFalse(vm.ApplyCalled, "OK без операции — нет-оп");
        }
    }
}
