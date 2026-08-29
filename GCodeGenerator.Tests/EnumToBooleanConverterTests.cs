#nullable enable
using System.Globalization;
using System.Windows.Data;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public sealed class EnumToBooleanConverterTests
    {
        private readonly EnumToBooleanConverter _converter = new EnumToBooleanConverter();

        [TestMethod]
        public void Convert_ChecksOnlyRadioButtonForCurrentEnumValue()
        {
            Assert.AreEqual(true, _converter.Convert(
                MillingDirection.Clockwise,
                typeof(bool),
                MillingDirection.Clockwise,
                CultureInfo.InvariantCulture));
            Assert.AreEqual(false, _converter.Convert(
                MillingDirection.CounterClockwise,
                typeof(bool),
                MillingDirection.Clockwise,
                CultureInfo.InvariantCulture));
            Assert.AreEqual(false, _converter.Convert(
                null!,
                typeof(bool),
                MillingDirection.Clockwise,
                CultureInfo.InvariantCulture));
            Assert.AreEqual(false, _converter.Convert(
                MillingDirection.Clockwise,
                typeof(bool),
                null!,
                CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void ConvertBack_CheckedRadioButtonWritesItsEnumValue()
        {
            var result = _converter.ConvertBack(
                true,
                typeof(MillingDirection),
                MillingDirection.CounterClockwise,
                CultureInfo.InvariantCulture);

            Assert.AreEqual(MillingDirection.CounterClockwise, result,
                "Выбранное направление должно попасть в операцию");
        }

        [TestMethod]
        public void ConvertBack_UncheckedOrInvalidValueDoesNotOverwriteSelection()
        {
            foreach (var value in new object?[] { false, null, "true" })
            {
                Assert.AreSame(
                    Binding.DoNothing,
                    _converter.ConvertBack(
                        value!,
                        typeof(PocketMode),
                        PocketMode.Island,
                        CultureInfo.InvariantCulture));
            }

            Assert.AreSame(
                Binding.DoNothing,
                _converter.ConvertBack(
                    true,
                    typeof(PocketMode),
                    null!,
                    CultureInfo.InvariantCulture));
        }
    }
}
