#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Состояние редактора при переключении кармана в остров.</summary>
    [TestClass]
    public sealed class PocketIslandEditorTests
    {
        [TestMethod]
        public void PocketModeChange_DisablesMachiningSettings()
        {
            var operation = new PocketCircleOperation();
            var editor = new PocketCircleOperationViewModel(null!);
            var changed = new List<string>();
            editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
            editor.Operation = operation;

            Assert.IsTrue(editor.IsMachiningPocket);

            changed.Clear();
            operation.PocketMode = PocketMode.Island;

            Assert.IsFalse(editor.IsMachiningPocket);
            CollectionAssert.Contains(changed, nameof(editor.IsMachiningPocket));
        }
    }
}
