using System;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class ProfileToolCompensationTests
    {
        [TestMethod]
        [DataRow(ToolPathMode.Outside, 12.0)]
        [DataRow(ToolPathMode.Inside, 8.0)]
        public void CircleProfile_OffsetsPathByFullToolRadius(ToolPathMode mode, double expectedRadius)
        {
            var operation = new ProfileCircleOperation
            {
                CenterX = 0,
                CenterY = 0,
                Radius = 10,
                ToolDiameter = 4,
                ToolPathMode = mode,
                TotalDepth = 1,
                StepDepth = 1,
                MaxSegmentLength = 0.5,
            };
            var settings = new GCodeSettings
            {
                Format = new GCodeFormatSettings
                {
                    AllowArcs = false,
                    UseComments = false,
                },
            };
            var program = Fixtures.OperationToolPath.Program(new UnifiedProfileGenerator(), operation, settings);

            var contourPoints = program.Blocks
                .Where(block => block.Words.Any(word => word.Letter == 'G' && word.Number == 1)
                    && block.Words.Any(word => word.Letter == 'X')
                    && block.Words.Any(word => word.Letter == 'Y'))
                .Select(block =>
                {
                    var x = block.Words.Single(word => word.Letter == 'X').Number;
                    var y = block.Words.Single(word => word.Letter == 'Y').Number;
                    return Math.Sqrt(x * x + y * y);
                })
                .ToList();

            Assert.IsTrue(contourPoints.Count > 0, "Профиль должен содержать линейные перемещения по контуру.");
            Assert.IsTrue(contourPoints.All(radius => Math.Abs(radius - expectedRadius) < 0.001),
                $"Ожидался радиус траектории {expectedRadius}, фактический диапазон: "
                + $"{contourPoints.Min():0.###}..{contourPoints.Max():0.###}.");
        }
    }
}
