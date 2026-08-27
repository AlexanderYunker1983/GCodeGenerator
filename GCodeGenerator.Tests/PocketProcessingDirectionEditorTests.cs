#nullable enable
using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.ViewModels.Pocket;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Состояние выбора направления обработки в редакторах карманов.</summary>
    [TestClass]
    public sealed class PocketProcessingDirectionEditorTests
    {
        [TestMethod]
        public void StrategyChange_UpdatesDirectionBlockAndKeepsExplicitChoice()
        {
            var operation = new PocketCircleOperation();
            var editor = new PocketCircleOperationViewModel(null!);
            var changed = new List<string>();
            editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
            editor.Operation = operation;

            Assert.IsTrue(editor.IsSpiralOrConcentricStrategy);
            Assert.AreEqual(PocketProcessingDirection.CenterOutward, operation.ProcessingDirection);

            changed.Clear();
            operation.PocketStrategy = PocketStrategy.Radial;

            Assert.IsFalse(editor.IsSpiralOrConcentricStrategy);
            CollectionAssert.Contains(changed, nameof(editor.IsSpiralOrConcentricStrategy));

            operation.PocketStrategy = PocketStrategy.Concentric;
            Assert.IsTrue(editor.IsSpiralOrConcentricStrategy);
            Assert.AreEqual(PocketProcessingDirection.OutsideIn, operation.ProcessingDirection,
                "без явного выбора используется прежний порядок концентрических проходов");

            operation.ProcessingDirection = PocketProcessingDirection.CenterOutward;
            operation.PocketStrategy = PocketStrategy.Spiral;
            operation.PocketStrategy = PocketStrategy.Concentric;
            Assert.AreEqual(PocketProcessingDirection.CenterOutward, operation.ProcessingDirection,
                "выбор пользователя не должен сбрасываться при смене стратегии");
        }
    }
}
