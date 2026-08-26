using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Чистовая обработка стенки выполняется на каждом слое до полной
    /// глубины. Черновой проход отступает от стенки на припуск в каждом
    /// слое, поэтому припуск лежит на всей высоте стенки — а доводился
    /// прежде только слой припуска у дна: выше него карман оставался
    /// уже задуманного на величину припуска, и заметить это можно было
    /// только промером детали.
    /// </summary>
    [TestClass]
    public class PocketWallFinishingTests
    {
        private const double Radius = 20.0;
        private const double ToolDiameter = 10.0;
        private const double Allowance = 0.5;
        private const double TotalDepth = 4.0;
        private const double StepDepth = 2.0;

        private static PocketCircleOperation CircleWithWallFinishing() => new PocketCircleOperation
        {
            CenterX = 0.0,
            CenterY = 0.0,
            Radius = Radius,
            PocketStrategy = PocketStrategy.Spiral,
            TotalDepth = TotalDepth,
            StepDepth = StepDepth,
            ContourHeight = 0.0,
            SafeZHeight = 5.0,
            ToolDiameter = ToolDiameter,
            StepPercentOfTool = 40.0,
            FeedXYRapid = 1000.0,
            FeedXYWork = 300.0,
            FeedZRapid = 500.0,
            FeedZWork = 200.0,
            Decimals = 3,
            WallTaperAngleDeg = 0.0,
            IsRoughingEnabled = true,
            IsFinishingEnabled = true,
            FinishingMode = PocketFinishingMode.Walls,
            FinishAllowance = Allowance,
        };

        /// <summary>
        /// Кромка фрезы касается стенки на каждой слоевой глубине. Точки
        /// стенки отличимы от черновых: чистовой контур идёт радиусом
        /// «стенка минус радиус фрезы», черновые проходы — ещё на припуск
        /// глубже внутрь.
        /// </summary>
        [TestMethod]
        public void WallPass_TouchesWallAtEveryLayerDepth()
        {
            var op = CircleWithWallFinishing();
            var toolPath = OperationToolPath.Build(new UnifiedPocketGenerator(), op, new GCodeSettings());

            var wallRadius = Radius - ToolDiameter / 2.0;
            var depthsWithWallContact = new HashSet<double>();

            var x = 0.0;
            var y = 0.0;
            var z = 0.0;
            foreach (var move in toolPath.Moves())
            {
                x = move.X ?? x;
                y = move.Y ?? y;
                z = move.Z ?? z;
                if (z >= -1e-9)
                    continue;

                var r = Math.Sqrt(x * x + y * y);
                if (Math.Abs(r - wallRadius) <= 0.05)
                    depthsWithWallContact.Add(Math.Round(z, 6));
            }

            var expectedDepths = new[] { -StepDepth, -TotalDepth };
            var reached = string.Join(", ", depthsWithWallContact.OrderByDescending(d => d)
                .Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            foreach (var depth in expectedDepths)
            {
                Assert.IsTrue(depthsWithWallContact.Contains(depth), FormattableString.Invariant(
                    $"на глубине {depth} нет касания стенки (радиус {wallRadius}); стенка доведена только на: {reached}"));
            }
        }
    }
}
