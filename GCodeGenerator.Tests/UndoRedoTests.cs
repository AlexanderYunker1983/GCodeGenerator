using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Отмена и повтор изменений списка операций (пункт 25 плана).
    ///
    /// Шаги записываются на существующих границах: добавление, удаление и
    /// перестановка — от самой коллекции, правка — одним шагом на диалог.
    /// Восстановленная операция — копия с тем же идентификатором: по нему
    /// её находят и предпросмотр, и последующие шаги истории.
    /// </summary>
    [TestClass]
    public class UndoRedoTests
    {
        private static DrillPointsOperation Drill(double x)
            => new DrillPointsOperation
            {
                Name = $"Drill {x}",
                Holes = { new DrillHole { X = x, Y = 0, TotalDepth = 2, StepDepth = 1 } }
            };

        [TestMethod]
        public void AddedOperation_UndoRemovesIt_RedoRestoresEquivalent()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            Assert.IsFalse(workspace.UndoCommand.CanExecute(null), "Пустая история — отменять нечего");

            var operation = Drill(10);
            workspace.AllOperations.Add(operation);
            Assert.IsTrue(workspace.UndoCommand.CanExecute(null));

            workspace.UndoCommand.Execute(null);
            Assert.AreEqual(0, workspace.AllOperations.Count, "Добавление отменено");
            Assert.IsTrue(workspace.RedoCommand.CanExecute(null));

            workspace.RedoCommand.Execute(null);
            Assert.AreEqual(1, workspace.AllOperations.Count, "Добавление повторено");
            var restored = (DrillPointsOperation)workspace.AllOperations[0];
            Assert.AreEqual(operation.Id, restored.Id, "Восстановлена та же операция документа");
            Assert.AreEqual(10, restored.Holes[0].X, "С тем же содержимым");
        }

        [TestMethod]
        public void RemovedOperation_UndoRestoresItAtTheSamePlace()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            var middle = Drill(2);
            workspace.AllOperations.Add(Drill(1));
            workspace.AllOperations.Add(middle);
            workspace.AllOperations.Add(Drill(3));

            workspace.SelectedOperation = middle;
            workspace.RemoveOperationCommand.Execute(null);
            Assert.AreEqual(2, workspace.AllOperations.Count);

            workspace.UndoCommand.Execute(null);

            Assert.AreEqual(3, workspace.AllOperations.Count, "Удаление отменено");
            Assert.AreEqual(middle.Id, workspace.AllOperations[1].Id, "Операция вернулась на своё место");
            Assert.AreSame(workspace.AllOperations[1], workspace.SelectedOperation,
                "Восстановленная операция снова выделена");

            workspace.RedoCommand.Execute(null);
            Assert.IsFalse(workspace.AllOperations.Any(op => op.Id == middle.Id), "Удаление повторено");
            Assert.IsNull(workspace.SelectedOperation, "Повторно удалённую операцию нельзя оставить выделенной");
        }

        [TestMethod]
        public void MovedOperation_UndoMovesItBack()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            var first = Drill(1);
            var second = Drill(2);
            workspace.AllOperations.Add(first);
            workspace.AllOperations.Add(second);

            workspace.SelectedOperation = first;
            workspace.MoveOperationDownCommand.Execute(null);
            Assert.AreSame(second, workspace.AllOperations[0], "Порядок изменён");

            workspace.UndoCommand.Execute(null);
            Assert.AreSame(first, workspace.AllOperations[0], "Перестановка отменена");

            workspace.RedoCommand.Execute(null);
            Assert.AreSame(second, workspace.AllOperations[0], "Перестановка повторена");
        }

        /// <summary>
        /// Правка — один шаг на диалог, сколько бы параметров он ни записал.
        /// Отмена подставляет копию прежнего состояния с тем же идентификатором.
        /// </summary>
        [TestMethod]
        public void EditedOperation_UndoRestoresValues_RedoReapplies()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            var operation = new ProfileCircleOperation { Radius = 10, ToolDiameter = 2 };
            workspace.AllOperations.Add(operation);
            workspace.SelectedOperation = operation;

            // «Пользователь» правит два параметра в открытом диалоге.
            dialogs.DialogAction = _ =>
            {
                operation.Radius = 42;
                operation.ToolDiameter = 4;
            };
            workspace.EditOperationCommand.Execute(null);
            Assert.AreEqual(42, operation.Radius);

            workspace.UndoCommand.Execute(null);
            var reverted = (ProfileCircleOperation)workspace.AllOperations[0];
            Assert.AreEqual(operation.Id, reverted.Id, "Та же операция документа");
            Assert.AreEqual(10, reverted.Radius, "Первый параметр возвращён");
            Assert.AreEqual(2, reverted.ToolDiameter, "Второй параметр возвращён тем же шагом");
            Assert.AreSame(reverted, workspace.SelectedOperation,
                "Ctrl+Z сохраняет выделение на восстановленном экземпляре");

            workspace.RedoCommand.Execute(null);
            var reapplied = (ProfileCircleOperation)workspace.AllOperations[0];
            Assert.AreEqual(42, reapplied.Radius, "Повтор вернул правку");
            Assert.AreEqual(4, reapplied.ToolDiameter);
            Assert.AreSame(reapplied, workspace.SelectedOperation,
                "Ctrl+Y сохраняет выделение на повторно восстановленном экземпляре");
        }

        /// <summary>
        /// Диалог, закрытый без изменений, шага не оставляет: отмена после
        /// него отменяет предыдущее действие, а не «пустую правку».
        /// </summary>
        [TestMethod]
        public void EditWithoutChanges_LeavesNoStep()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            var operation = Drill(10);
            workspace.AllOperations.Add(operation);
            workspace.SelectedOperation = operation;

            dialogs.DialogAction = null; // окно открыли и закрыли
            workspace.EditOperationCommand.Execute(null);

            workspace.UndoCommand.Execute(null);
            Assert.AreEqual(0, workspace.AllOperations.Count,
                "Отменилось добавление операции: пустая правка шага не оставила");
        }

        /// <summary>
        /// Новое изменение делает «повторить» бессмысленным: повторялась бы
        /// ветка истории, которой больше нет.
        /// </summary>
        [TestMethod]
        public void NewChangeAfterUndo_DropsRedo()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            workspace.AllOperations.Add(Drill(1));
            workspace.UndoCommand.Execute(null);
            Assert.IsTrue(workspace.RedoCommand.CanExecute(null));

            workspace.AllOperations.Add(Drill(2));

            Assert.IsFalse(workspace.RedoCommand.CanExecute(null), "Повтор недоступен после новой правки");
        }

        /// <summary>
        /// Замена документа — не правка, а другой документ: история прежнего
        /// к нему не относится и очищается.
        /// </summary>
        [TestMethod]
        public async Task NewProject_ClearsHistory()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;
            workspace.AllOperations.Add(Drill(1));
            Assert.IsTrue(workspace.UndoCommand.CanExecute(null));
            dialogs.SaveConfirmationResult = GCodeGenerator.Services.SaveConfirmation.Discard;

            await ((IAsyncRelayCommand)main.ProjectWorkflow.NewProgramCommand).ExecuteAsync(null);

            Assert.IsFalse(workspace.UndoCommand.CanExecute(null), "История прежнего документа очищена");
            Assert.IsFalse(workspace.RedoCommand.CanExecute(null));
        }

        // ------------------------------------------------------------------
        // Предел глубины
        // ------------------------------------------------------------------

        /// <summary>
        /// История не растёт без предела. Шаг хранит состояние операции
        /// сериализованным, и у операции с контуром из чертежа это сотни
        /// килобайт: за долгий сеанс история заняла бы больше самого
        /// документа, причём молча.
        /// </summary>
        [TestMethod]
        public void History_StopsGrowingAtItsLimit()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;

            for (var step = 0; step < OperationHistory.MaxSteps + 25; step++)
                workspace.AllOperations.Add(Drill(step));

            Assert.AreEqual(OperationHistory.MaxSteps, workspace.History.UndoCount,
                "Глубина истории ограничена");
        }

        /// <summary>
        /// Отбрасывается самый ранний шаг, а не последний: отменять начинают
        /// с того, что сделали только что.
        /// </summary>
        [TestMethod]
        public void History_ForgetsTheOldestStepFirst()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;

            for (var step = 0; step < OperationHistory.MaxSteps + 3; step++)
                workspace.AllOperations.Add(Drill(step));

            // Отменяем всё, что помнит история: уходят последние добавления,
            // а первые три операции остаются — их шаги забыты.
            for (var step = 0; step < OperationHistory.MaxSteps; step++)
                workspace.UndoCommand.Execute(null);

            Assert.IsFalse(workspace.UndoCommand.CanExecute(null), "История исчерпана");
            Assert.AreEqual(3, workspace.AllOperations.Count,
                "Операции, чьи шаги забыты, остаются в документе");
            CollectionAssert.AreEqual(
                new[] { "Drill 0", "Drill 1", "Drill 2" },
                workspace.AllOperations.Select(operation => operation.Name).ToArray(),
                "Забыты именно самые ранние шаги");
        }

        /// <summary>
        /// Повтор после отмен возвращает шаги на место и не выходит за предел.
        /// </summary>
        [TestMethod]
        public void History_StaysWithinItsLimit_AfterUndoAndRedo()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var workspace = main.OperationsWorkspace;

            for (var step = 0; step < OperationHistory.MaxSteps; step++)
                workspace.AllOperations.Add(Drill(step));

            for (var step = 0; step < 10; step++)
                workspace.UndoCommand.Execute(null);
            for (var step = 0; step < 10; step++)
                workspace.RedoCommand.Execute(null);

            Assert.AreEqual(OperationHistory.MaxSteps, workspace.History.UndoCount);
            Assert.AreEqual(OperationHistory.MaxSteps, workspace.AllOperations.Count,
                "Повтор вернул все операции");
        }
    }
}
