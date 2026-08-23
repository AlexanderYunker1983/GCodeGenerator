using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты roughing/finishing карманов (пункт 5.7 плана).
    /// Проверяют геометрию траектории:
    /// - черновая останавливается на глубине TotalDepth − припуск и оставляет припуск по контуру;
    /// - чистовая (Walls/Bottom/All) доходит до полной глубины;
    /// - Walls: кромка фрезы на стенке; Bottom: траектория смещена внутрь на припуск;
    /// - «слишком маленький после припуска» → комментарий без фрезеровки;
    /// - припуск больше глубины — без сбоев (ограничение припуска).
    ///
    /// Механизм припуска — увеличение диаметра фрезы (черновая и Bottom),
    /// поэтому радиус траектории центра = R − (r + припуск).
    /// </summary>
    [TestClass]
    public class PocketRoughingFinishingTests
    {
        private const double R = 20.0;
        private const double ToolDiameter = 10.0;
        private const double r = ToolDiameter / 2.0;
        private const double Allowance = 2.0;
        private const double TotalDepth = 10.0;
        private const double StepDepth = 2.0;

        private static PocketCircleOperation Circle(
            bool roughing = true, bool finishing = false,
            PocketFinishingMode mode = PocketFinishingMode.All,
            double radius = R, double totalDepth = TotalDepth)
        {
            return new PocketCircleOperation
            {
                CenterX = 0.0, CenterY = 0.0, Radius = radius,
                TotalDepth = totalDepth, StepDepth = StepDepth,
                ContourHeight = 0.0, SafeZHeight = 5.0,
                ToolDiameter = ToolDiameter, StepPercentOfTool = 40.0,
                FeedXYRapid = 1000.0, FeedXYWork = 300.0,
                FeedZRapid = 500.0, FeedZWork = 200.0,
                Decimals = 3, WallTaperAngleDeg = 0.0,
                IsRoughingEnabled = roughing,
                IsFinishingEnabled = finishing,
                FinishAllowance = Allowance,
                FinishingMode = mode,
            };
        }

        private static List<string> Run(OperationBase op)
        {
            var program = new GCodeProgram();
            new UnifiedPocketGenerator().Generate(op, new ProgramBuilder(program), new GCodeSettings { UseComments = true });
            GCodeFormatter.Format(program, new GCodeSettings { UseLineNumbers = false, UseComments = true });
            return program.Lines.ToList();
        }

        private static double MaxToolCenterRadius(List<string> lines)
        {
            double max = 0;
            foreach (var m in GCodeLineParser.LinearXyMoves(lines))
            {
                double rad = Math.Sqrt(m.X.Value * m.X.Value + m.Y.Value * m.Y.Value);
                if (rad > max) max = rad;
            }
            return max;
        }

        // ------------------------------------------------------------------
        // Черновая обработка
        // ------------------------------------------------------------------

        [TestMethod]
        public void RoughingOnly_StopsAtAllowanceDepth()
        {
            var lines = Run(Circle(roughing: true, finishing: false));
            var minZ = GCodeLineParser.MinZ(lines);
            Assert.AreEqual(-(TotalDepth - Allowance), minZ.Value, 1e-6,
                "Черновая должна остановиться на глубине TotalDepth − припуск");
        }

        [TestMethod]
        public void RoughingOnly_LeavesWallBand()
        {
            var lines = Run(Circle(roughing: true, finishing: false));
            double expectedOuterRadius = R - r - Allowance; // центр фрезы: R − (r + припуск)
            double maxRadius = MaxToolCenterRadius(lines);
            Assert.IsTrue(maxRadius > expectedOuterRadius - 0.5,
                $"Черновая должна дойти до {expectedOuterRadius:F2} (радиус центра), получено {maxRadius:F2}");
            Assert.IsTrue(maxRadius < R - r,
                $"Черновая не должна доходить до стенки (радиус центра < {R - r:F2}), получено {maxRadius:F2}");
        }

        // ------------------------------------------------------------------
        // Чистовая обработка: глубина
        // ------------------------------------------------------------------

        [TestMethod]
        [DataRow(PocketFinishingMode.Walls)]
        [DataRow(PocketFinishingMode.Bottom)]
        [DataRow(PocketFinishingMode.All)]
        public void Finishing_ReachesFullDepth(PocketFinishingMode mode)
        {
            var lines = Run(Circle(roughing: true, finishing: true, mode: mode));
            var minZ = GCodeLineParser.MinZ(lines);
            Assert.AreEqual(-TotalDepth, minZ.Value, 1e-6,
                $"Чистовая ({mode}) должна дойти до полной глубины");
        }

        // ------------------------------------------------------------------
        // Чистовая обработка: геометрия
        // ------------------------------------------------------------------

        [TestMethod]
        public void FinishingWalls_ToolEdgeOnWall()
        {
            // Только чистовая Walls: кромка фрезы на стенке → радиус центра = R − r.
            var lines = Run(Circle(roughing: false, finishing: true, mode: PocketFinishingMode.Walls));
            double maxRadius = MaxToolCenterRadius(lines);
            Assert.IsTrue(Math.Abs(maxRadius - (R - r)) < 0.5,
                $"Walls: радиус центра должен быть ≈ {R - r:F2} (кромка на стенке), получено {maxRadius:F2}");
        }

        [TestMethod]
        public void FinishingBottom_OffsetInwardByAllowance()
        {
            // Только чистовая Bottom: траектория смещена внутрь на припуск → радиус центра = R − r − припуск.
            var lines = Run(Circle(roughing: false, finishing: true, mode: PocketFinishingMode.Bottom));
            double maxRadius = MaxToolCenterRadius(lines);
            double expected = R - r - Allowance;
            Assert.IsTrue(Math.Abs(maxRadius - expected) < 0.5,
                $"Bottom: радиус центра должен быть ≈ {expected:F2}, получено {maxRadius:F2}");
        }

        // ------------------------------------------------------------------
        // Краевые случаи
        // ------------------------------------------------------------------

        [TestMethod]
        public void TooSmallAfterAllowance_SkipsWithComment()
        {
            // Радиус 6, фреза 10 (r=5), припуск 2 → радиус центра черновой = 6 − 7 = −1 < 0.
            var lines = Run(Circle(roughing: true, finishing: false, radius: 6.0));
            Assert.IsTrue(lines.Any(l => l.Contains("Pocket too small after roughing allowance, skipping")),
                "Ожидался комментарий о пропуске кармана");
            Assert.AreEqual(0, GCodeLineParser.LinearXyMoves(lines).Count,
                "Фрезеровки быть не должно");
        }

        [TestMethod]
        public void AllowanceGreaterThanTotalDepth_Clamped_NoCrash()
        {
            // Припуск 5 > глубины 2: припуск по глубине ограничивается глубиной,
            // генерация завершается без исключений, чистовая доходит до дна.
            var op = Circle(roughing: true, finishing: true, mode: PocketFinishingMode.All, totalDepth: 2.0);
            op.FinishAllowance = 5.0;
            var lines = Run(op);
            var minZ = GCodeLineParser.MinZ(lines);
            Assert.AreEqual(-2.0, minZ.Value, 1e-6,
                "Обработка должна дойти до полной глубины");
        }
    }
}
