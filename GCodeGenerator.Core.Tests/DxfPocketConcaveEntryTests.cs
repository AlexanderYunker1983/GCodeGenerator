using System;
using System.Collections.Generic;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Врезание в вогнутую область кармана. Центроид подковы лежит в её
    /// выемке — вне самой области, — а карманы из чертежа существуют ради
    /// именно таких контуров. Прежде точка врезания не проверялась на
    /// принадлежность области: инструмент опускался на глубину в центроид,
    /// то есть в нетронутый материал за стенкой кармана.
    /// </summary>
    [TestClass]
    public class DxfPocketConcaveEntryTests
    {
        private const double ToolRadius = 3.0;

        /// <summary>
        /// П-образный контур 40×30 с выемкой 20×20 сверху: нижняя перекладина
        /// и два рукава шириной 10. Центроид — в выемке.
        /// </summary>
        private static PocketDxfOperation HorseshoePocket() => new PocketDxfOperation
        {
            ClosedContours = new List<Polyline2D> { Horseshoe() },
            TotalDepth = 2.0,
            StepDepth = 2.0,
            ContourHeight = 0.0,
            SafeZHeight = 5.0,
            ToolDiameter = ToolRadius * 2.0,
            StepPercentOfTool = 40.0,
            FeedXYRapid = 1000.0,
            FeedXYWork = 300.0,
            FeedZRapid = 500.0,
            FeedZWork = 200.0,
            Decimals = 3,
            WallTaperAngleDeg = 0.0,
            IsRoughingEnabled = false,
            IsFinishingEnabled = false,
            PocketStrategy = PocketStrategy.ZigZag,
        };

        private static Polyline2D Horseshoe() => new Polyline2D
        {
            Points =
            {
                new Point2D { X = 0.0, Y = 0.0 },
                new Point2D { X = 40.0, Y = 0.0 },
                new Point2D { X = 40.0, Y = 30.0 },
                new Point2D { X = 30.0, Y = 30.0 },
                new Point2D { X = 30.0, Y = 10.0 },
                new Point2D { X = 10.0, Y = 10.0 },
                new Point2D { X = 10.0, Y = 30.0 },
                new Point2D { X = 0.0, Y = 30.0 },
                new Point2D { X = 0.0, Y = 0.0 },
            },
        };

        /// <summary>
        /// Каждое врезание лежит внутри смещённой области кармана. Прежде
        /// врезание шло в центроид области — у подковы он в выемке, за
        /// стенкой, и проверка находила врезание вне области.
        /// </summary>
        [TestMethod]
        public void EveryPlunge_LandsInsideOffsetArea()
        {
            var op = HorseshoePocket();
            Assert.AreEqual(0, op.Validate().Count, "фикстура-подкова должна быть пригодной");

            var offsetParts = ContourOffset.Offset(Horseshoe().Points, -ToolRadius);
            Assert.AreEqual(1, offsetParts.Count, "рукава шире диаметра фрезы: область одна");
            var offsetContour = offsetParts[0];

            var centroid = Geometry2D.Centroid(offsetContour, GeometryTolerances.Vertex);
            Assert.IsFalse(Geometry2D.IsPointInsidePolygon(centroid.x, centroid.y, offsetContour),
                "смысл фикстуры: центроид подковы обязан лежать вне области — иначе тест ничего не проверяет");

            var toolPath = OperationToolPath.Build(new UnifiedPocketGenerator(), op, new GCodeSettings());

            var x = 0.0;
            var y = 0.0;
            var z = 0.0;
            var plunges = 0;
            foreach (var move in toolPath.Moves())
            {
                var targetZ = move.Z ?? z;
                if (targetZ < z - 1e-9 && targetZ < -1e-9)
                {
                    Assert.IsTrue(Geometry2D.IsPointInsidePolygon(x, y, offsetContour),
                        FormattableString.Invariant(
                            $"врезание на глубину {targetZ} в точке ({x:0.###}; {y:0.###}) — вне области кармана"));
                    plunges++;
                }

                x = move.X ?? x;
                y = move.Y ?? y;
                z = targetZ;
            }

            Assert.IsTrue(plunges > 0, "в программе должно быть хотя бы одно врезание");
        }
    }
}
