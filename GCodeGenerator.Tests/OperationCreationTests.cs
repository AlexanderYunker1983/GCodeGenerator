using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Добавление операции — сделка с подтверждением: операция попадает
    /// в документ только по OK.
    ///
    /// Раньше кнопка вкладки сначала добавляла операцию со значениями по
    /// умолчанию и лишь потом открывала окно, поэтому отмена оставляла
    /// в проекте операцию, которую пользователь заводить передумал, — а для
    /// карманов и контуров по чертежу ещё и заведомо непригодную к генерации,
    /// без единого контура.
    /// </summary>
    [TestClass]
    public class OperationCreationTests
    {
        [TestMethod]
        public void Cancel_LeavesDocumentEmpty()
        {
            var operations = new ObservableCollection<OperationBase>();
            var editors = new FakeEditorIndex { Factory = _ => new PocketCircleOperationViewModel(null) };
            var dialogs = new FakeDialogs
            {
                DialogAction = vm => ((PocketCircleOperationViewModel)vm).CancelCommand.Execute(null),
            };

            var added = new OperationEditorFactory(editors, dialogs)
                .CreateOperation(new PocketCircleOperation(), operations);

            Assert.IsFalse(added, "Отменённая операция не считается добавленной");
            Assert.AreEqual(0, operations.Count, "Отмена не оставляет операцию в документе");
        }

        [TestMethod]
        public void Ok_AddsOperationWithEnteredValues()
        {
            var operations = new ObservableCollection<OperationBase>();
            var editors = new FakeEditorIndex { Factory = _ => new PocketCircleOperationViewModel(null) };
            var dialogs = new FakeDialogs
            {
                DialogAction = vm =>
                {
                    var editor = (PocketCircleOperationViewModel)vm;
                    editor.Operation.Radius = 33;
                    editor.OkCommand.Execute(null);
                },
            };

            var operation = new PocketCircleOperation();
            var added = new OperationEditorFactory(editors, dialogs).CreateOperation(operation, operations);

            Assert.IsTrue(added, "Подтверждённая операция добавлена");
            Assert.AreEqual(1, operations.Count, "Операция в документе одна");
            Assert.AreSame(operation, operations[0], "В документ попадает та самая операция");
            Assert.AreEqual(33.0, ((PocketCircleOperation)operations[0]).Radius, 1e-9,
                "Введённые в окне значения сохранены");
        }

        /// <summary>
        /// Неверные параметры новой операции окно не закрывают; когда
        /// пользователь всё же закрывает его, в документе ничего не остаётся.
        /// </summary>
        [TestMethod]
        public void InvalidParameters_LeaveDocumentEmpty()
        {
            var operations = new ObservableCollection<OperationBase>();
            var editors = new FakeEditorIndex { Factory = _ => new PocketCircleOperationViewModel(null) };
            var dialogs = new FakeDialogs
            {
                DialogAction = vm =>
                {
                    var editor = (PocketCircleOperationViewModel)vm;
                    editor.Operation.Radius = 0;
                    editor.OkCommand.Execute(null);
                    Assert.IsTrue(editor.HasValidationError, "Окно сообщает о неверных параметрах");
                },
            };

            var added = new OperationEditorFactory(editors, dialogs)
                .CreateOperation(new PocketCircleOperation(), operations);

            Assert.IsFalse(added, "Непринятая операция не добавляется");
            Assert.AreEqual(0, operations.Count, "Документ остаётся пустым");
        }

        /// <summary>
        /// Кнопки вкладок работают через ту же сделку: отмена не меняет
        /// документ, подтверждение добавляет операцию и выделяет её.
        /// </summary>
        [TestMethod]
        public void CategoryTab_AddCommand_RespectsCancel()
        {
            var editors = new FakeEditorIndex { Factory = _ => new PocketCircleOperationViewModel(null) };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(editors: editors);
            dialogs.DialogAction = vm => ((PocketCircleOperationViewModel)vm).CancelCommand.Execute(null);

            FindAddCommand(main).Execute(null);

            Assert.AreEqual(0, main.OperationsWorkspace.AllOperations.Count, "Отмена не добавляет операцию");
            Assert.IsNull(main.OperationsWorkspace.SelectedOperation, "Выделять нечего");
        }

        [TestMethod]
        public void CategoryTab_AddCommand_SelectsAcceptedOperation()
        {
            var editors = new FakeEditorIndex { Factory = _ => new PocketCircleOperationViewModel(null) };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(editors: editors);
            dialogs.DialogAction = vm => ((PocketCircleOperationViewModel)vm).OkCommand.Execute(null);

            FindAddCommand(main).Execute(null);

            Assert.AreEqual(1, main.OperationsWorkspace.AllOperations.Count, "Подтверждённая операция добавлена");
            Assert.AreSame(main.OperationsWorkspace.AllOperations[0], main.OperationsWorkspace.SelectedOperation, "Новая операция выделена");
        }

        private static ICommand FindAddCommand(MainViewModel main)
            => main.OperationsWorkspace.PocketOperations.GetType()
                .GetProperties()
                .Where(p => p.Name == "AddPocketCircleCommand")
                .Select(p => (ICommand)p.GetValue(main.OperationsWorkspace.PocketOperations))
                .Single();
    }
}
