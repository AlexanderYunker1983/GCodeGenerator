using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Состояние полей винтового подвода в редакторах карманов.</summary>
    [TestClass]
    public class PocketEntryEditorTests
    {
        [TestMethod]
        public void EntryModeChange_UpdatesHelicalFieldsState()
        {
            var operation = new PocketCircleOperation();
            var editor = new PocketCircleOperationViewModel(null);
            var changed = new List<string>();
            editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            editor.Operation = operation;

            Assert.IsFalse(editor.IsHelicalEntry, "по умолчанию вход вертикальный");

            changed.Clear();
            operation.EntryMode = PocketEntryMode.Helical;

            Assert.IsTrue(editor.IsHelicalEntry);
            CollectionAssert.Contains(changed, nameof(editor.IsHelicalEntry));
        }
    }
}
