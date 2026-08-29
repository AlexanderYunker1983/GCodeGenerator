#nullable enable
using System;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Аналитическая геометрия профиля со скруглёнными углами.</summary>
    [TestClass]
    public sealed class RoundedRectangleProfileGeometryTests
    {
        [TestMethod]
        public void Perimeter_SubtractsCornerRadiiFromStraightSegments()
        {
            var operation = new ProfileRoundedRectangleOperation
            {
                Width = 40,
                Height = 20,
                RadiusTopLeft = 1,
                RadiusTopRight = 2,
                RadiusBottomRight = 3,
                RadiusBottomLeft = 4
            };
            var geometry = new RoundedRectangleProfileGeometry(operation);

            var radiusSum = 1.0 + 2.0 + 3.0 + 4.0;
            var expected = 2 * (operation.Width + operation.Height)
                - 2 * radiusSum
                + Math.PI / 2 * radiusSum;

            Assert.AreEqual(expected, geometry.GetPerimeter(toolOffset: 0), 1e-9,
                "Прямые участки должны быть короче полных сторон на два соседних радиуса");
        }
    }
}
