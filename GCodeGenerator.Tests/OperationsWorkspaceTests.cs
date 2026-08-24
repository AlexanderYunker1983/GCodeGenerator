using System;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class OperationsWorkspaceTests
    {
        private sealed class StubThemeService : IThemeService
        {
            public event EventHandler ThemeChanged;

            public void ApplyTheme(bool useDarkTheme)
            {
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [TestMethod]
        public void SelectionAndContentChanges_HaveSeparateSignalsAndSynchronizedCommands()
        {
            var dialogs = new MainViewModelOperationEditTests.RecordingDialogService();
            var workspace = new OperationsWorkspaceViewModel(
                null,
                new OperationEditorFactory(dialogs),
                new StubThemeService());
            var first = new ProfileCircleOperation { Name = "First" };
            var second = new ProfileCircleOperation { Name = "Second" };
            var contentChanges = 0;
            workspace.ContentChanged += (_, _) => contentChanges++;

            workspace.AllOperations.Add(first);
            workspace.AllOperations.Add(second);
            Assert.AreEqual(2, contentChanges);

            workspace.OperationsPreview.SelectedOperation = second;
            Assert.AreSame(second, workspace.SelectedOperation);
            Assert.AreEqual(2, contentChanges, "Selection is UI state, not project content");
            Assert.IsTrue(workspace.MoveOperationUpCommand.CanExecute(null));
            Assert.IsFalse(workspace.MoveOperationDownCommand.CanExecute(null));

            second.Name = "Renamed";
            Assert.AreEqual(3, contentChanges);

            workspace.MoveOperationUpCommand.Execute(null);
            Assert.AreSame(second, workspace.AllOperations[0]);
            Assert.AreEqual(4, contentChanges);
            Assert.AreSame(second, workspace.SelectedOperation,
                "Moving an item must preserve the current selection");

            second.Name = "Renamed after move";
            Assert.AreEqual(5, contentChanges,
                "Moving an item must preserve its PropertyChanged subscription");

            workspace.RemoveOperationCommand.Execute(null);
            Assert.AreEqual(6, contentChanges);
            Assert.IsNull(workspace.SelectedOperation);
            Assert.AreSame(first, workspace.AllOperations[0]);
            Assert.IsFalse(workspace.RemoveOperationCommand.CanExecute(null));
        }

        [TestMethod]
        public void Clear_DetachesRemovedOperationsAndClearsSelection()
        {
            var dialogs = new MainViewModelOperationEditTests.RecordingDialogService();
            var workspace = new OperationsWorkspaceViewModel(
                null,
                new OperationEditorFactory(dialogs),
                new StubThemeService());
            var operation = new ProfileCircleOperation();
            workspace.AllOperations.Add(operation);
            workspace.SelectedOperation = operation;
            var contentChanges = 0;
            workspace.ContentChanged += (_, _) => contentChanges++;

            workspace.AllOperations.Clear();

            Assert.IsNull(workspace.SelectedOperation);
            Assert.AreEqual(1, contentChanges);

            operation.Name = "Changed after removal";
            Assert.AreEqual(1, contentChanges,
                "Removed operations must no longer notify the workspace");
        }
    }
}
