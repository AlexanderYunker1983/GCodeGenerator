using System.Windows.Input;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Диалог до того, как ему дали операцию.
    ///
    /// Окно создаётся контейнером, а операцию получает следующим шагом —
    /// между этими моментами его команды формально доступны. Раньше вызов
    /// любой из них в этот промежуток обрывался отказом обращения к пустоте;
    /// проверка ссылок на пустоту вывела эти места на свет, и теперь команда
    /// просто ничего не делает.
    /// </summary>
    [TestClass]
    public class EditorWithoutOperationTests
    {
        [TestMethod]
        public void DrillHoleCommands_DoNothingWithoutOperation()
        {
            var dialog = new DrillPointsOperationViewModel(null);

            Execute(dialog.AddHoleCommand);
            Execute(dialog.RemoveHoleCommand);
            Execute(dialog.MoveHoleUpCommand);
            Execute(dialog.MoveHoleDownCommand);

            Assert.IsNull(dialog.Operation, "Операции так и не появилось");
            Assert.IsNull(dialog.SelectedHole, "Выделять нечего");
        }

        /// <summary>
        /// Диалог чертежа заводит операцию сам: он открывается и сам по себе,
        /// а не только из потока добавления, и импортировать тогда есть куда.
        /// </summary>
        [TestMethod]
        public void DxfDialog_CreatesItsOwnOperation()
        {
            var dialog = new PocketDxfOperationViewModel(null, null, null, new DxfImportService());

            Assert.IsNotNull(dialog.Operation, "Операция есть сразу после создания окна");
        }

        /// <summary>
        /// Подтверждение без операции тоже безвредно: диалог не считается
        /// принятым, потому что принимать нечего.
        /// </summary>
        [TestMethod]
        public void Ok_WithoutOperation_DoesNotAccept()
        {
            var dialog = new PocketCircleOperationViewModel(null);

            Execute(dialog.OkCommand);

            Assert.IsFalse(dialog.IsAccepted);
        }

        /// <summary>
        /// Как только операция задана, команды работают: проверка на пустоту
        /// не должна была отключить сам диалог.
        /// </summary>
        [TestMethod]
        public void WithOperation_HoleCommandsWork()
        {
            var dialog = new DrillPointsOperationViewModel(null);
            var operation = new DrillPointsOperation();
            ((GCodeGenerator.ViewModels.IOperationEditorViewModel)dialog).SetOperation(operation);
            var before = operation.Holes.Count;

            Execute(dialog.AddHoleCommand);

            Assert.AreEqual(before + 1, operation.Holes.Count, "Отверстие добавлено");
            Assert.IsNotNull(dialog.SelectedHole, "И выделено");
        }

        private static void Execute(ICommand command)
        {
            if (command.CanExecute(null))
                command.Execute(null);
        }
    }
}
