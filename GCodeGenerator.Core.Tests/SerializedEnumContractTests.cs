using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Числовые значения перечислений являются частью публичного формата
    /// .ygc: System.Text.Json записывает их числами. Вставка нового элемента
    /// в середину enum без явных значений раньше молча меняла смысл уже
    /// сохранённых проектов — например, Outside мог превратиться в Inside.
    /// </summary>
    [TestClass]
    public sealed class SerializedEnumContractTests
    {
        private static readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, int>> Contract =
            new Dictionary<Type, IReadOnlyDictionary<string, int>>
            {
                [typeof(ToolPathMode)] = Values(("OnLine", 0), ("Outside", 1), ("Inside", 2)),
                [typeof(ReferencePointType)] = Values(
                    ("Center", 0), ("TopLeft", 1), ("TopRight", 2), ("BottomLeft", 3), ("BottomRight", 4)),
                [typeof(PocketStrategy)] = Values(
                    ("Concentric", 0), ("Spiral", 1), ("Radial", 2), ("ZigZag", 3), ("Lines", 4)),
                [typeof(MillingDirection)] = Values(("Clockwise", 0), ("CounterClockwise", 1)),
                [typeof(PocketProcessingDirection)] = Values(("CenterOutward", 0), ("OutsideIn", 1)),
                [typeof(DrillMode)] = Values(
                    ("Points", 0), ("Line", 1), ("Array", 2), ("Rect", 3), ("Circle", 4),
                    ("Arc", 5), ("Polygon", 6), ("Ellipse", 7), ("Package", 8)),
                [typeof(EntryMode)] = Values(("Vertical", 0), ("Angled", 1)),
                [typeof(PocketMode)] = Values(("Machining", 0), ("Island", 1)),
                [typeof(PocketFinishingMode)] = Values(("Walls", 0), ("Bottom", 1), ("All", 2)),
                [typeof(PocketEntryMode)] = Values(("Vertical", 0), ("Helical", 1)),
                [typeof(OuterBoundaryType)] = Values(("Rectangle", 0), ("Ellipse", 1)),
            };

        [TestMethod]
        public void NumericValues_AreFrozenForProjectFileCompatibility()
        {
            Assert.AreEqual(11, Contract.Count, "таблица охватывает все enum, сохраняемые в .ygc");

            foreach (var pair in Contract)
            {
                var actual = Enum.GetNames(pair.Key)
                    .ToDictionary(
                        name => name,
                        name => Convert.ToInt32(Enum.Parse(pair.Key, name)));

                CollectionAssert.AreEquivalent(
                    pair.Value.Keys.ToArray(),
                    actual.Keys.ToArray(),
                    $"{pair.Key.Name}: добавление или удаление элемента меняет контракт формата");

                foreach (var expected in pair.Value)
                    Assert.AreEqual(expected.Value, actual[expected.Key], $"{pair.Key.Name}.{expected.Key}");
            }
        }

        private static IReadOnlyDictionary<string, int> Values(params (string Name, int Value)[] values)
            => values.ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
    }
}
