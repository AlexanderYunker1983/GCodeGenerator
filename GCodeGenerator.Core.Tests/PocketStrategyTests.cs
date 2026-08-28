using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Поведенческие тесты стратегий фрезерования карманов (пункт 5.7 плана).
    /// Фаза 5 — новая функциональность: вывод не сравнивается со старым golden,
    /// а покрывается собственными тестами геометрии траектории:
    /// - покрытие (кромка у стенки + центр, проходы до дна);
    /// - отсутствие выхода за контур (центр инструмента внутри смещённого контура);
    /// - Z-переходы (Lines — отвод перед каждым резом; остальные — без Z-движений в слое);
    /// - интеграция «стратегия × тип кармана» (круг, прямоугольник, эллипс, DXF);
    /// - направление CW/CCW (Concentric).
    ///
    /// Все тесты — однослойные карманы (TotalDepth = StepDepth), чтобы изолировать
    /// поведение стратегии в одном слое.
    /// </summary>
    [TestClass]
    public class PocketStrategyTests
    {
        // Общие параметры однослойного кармана.
        private const double ContourHeight = 0.0;
        private const double TotalDepth = 2.0;
        private const double StepDepth = 2.0; // один слой
        private const double SafeZ = 5.0;
        private const double ToolDiameter = 10.0;
        private const double StepPercent = 40.0; // step = 4.0

        private static readonly PocketStrategy[] AllStrategies =
        {
            PocketStrategy.Concentric,
            PocketStrategy.Spiral,
            PocketStrategy.Radial,
            PocketStrategy.ZigZag,
            PocketStrategy.Lines,
        };

        // ------------------------------------------------------------------
        // Фабрики операций и генерация
        // ------------------------------------------------------------------

        private static T Configure<T>(T op, PocketStrategy strategy) where T : PocketOperationBase
        {
            op.PocketStrategy = strategy;
            op.TotalDepth = TotalDepth;
            op.StepDepth = StepDepth;
            op.ContourHeight = ContourHeight;
            op.SafeZHeight = SafeZ;
            op.ToolDiameter = ToolDiameter;
            op.StepPercentOfTool = StepPercent;
            op.FeedXYRapid = 1000.0;
            op.FeedXYWork = 300.0;
            op.FeedZRapid = 500.0;
            op.FeedZWork = 200.0;
            // 6 знаков: точность проверки «внутри контура» (допуск IsPointInside 1e-6)
            op.Decimals = 6;
            op.WallTaperAngleDeg = 0.0;
            op.LineAngleDeg = 0.0;
            op.IsRoughingEnabled = false;
            op.IsFinishingEnabled = false;
            return op;
        }

        private static PocketCircleOperation Circle() => new PocketCircleOperation
        {
            CenterX = 0.0, CenterY = 0.0, Radius = 20.0,
        };

        private static PocketRectangleOperation Rectangle() => new PocketRectangleOperation
        {
            Width = 40.0, Height = 20.0,
            ReferencePointX = 0.0, ReferencePointY = 0.0,
            ReferencePointType = ReferencePointType.Center,
        };

        private static PocketEllipseOperation Ellipse() => new PocketEllipseOperation
        {
            CenterX = 0.0, CenterY = 0.0,
            RadiusX = 15.0, RadiusY = 8.0, RotationAngle = 0.0,
        };

        private static PocketDxfOperation Dxf()
        {
            var op = new PocketDxfOperation
            {
                DxfFilePath = DxfFixtureLoader.GetAssetPath("pocket_sample.dxf"),
            };
            op.ClosedContours = DxfFixtureLoader.LoadPocketClosedContours("pocket_sample.dxf");
            return op;
        }

        private static List<string> Run(OperationBase op)
        {
            return Fixtures.OperationToolPath.Program(
                    new UnifiedPocketGenerator(),
                    op,
                    new GCodeSettings { Format = new GCodeFormatSettings { UseComments = true } },
                    new GCodeSettings { Format = new GCodeFormatSettings { UseLineNumbers = false, UseComments = true } })
                .Lines.ToList();
        }

        private static double Step => ToolDiameter * StepPercent / 100.0;

        // ------------------------------------------------------------------
        // Интеграция: стратегия × тип кармана
        // ------------------------------------------------------------------

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_MillsPocket_Circle(PocketStrategy strategy)
            => AssertMillsPocket(Configure(Circle(), strategy), -TotalDepth);

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_MillsPocket_Rectangle(PocketStrategy strategy)
            => AssertMillsPocket(Configure(Rectangle(), strategy), -TotalDepth);

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_MillsPocket_Ellipse(PocketStrategy strategy)
            => AssertMillsPocket(Configure(Ellipse(), strategy), -TotalDepth);

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_MillsPocket_Dxf(PocketStrategy strategy)
            => AssertMillsPocket(Configure(Dxf(), strategy), -TotalDepth);

        private static void AssertMillsPocket(OperationBase op, double expectedFinalZ)
        {
            var lines = Run(op);
            var linearXy = GCodeLineParser.LinearXyMoves(lines);
            Assert.IsTrue(linearXy.Count > 0, "Ожидались G1-перемещения XY (фрезеровка)");
            var minZ = GCodeLineParser.MinZ(lines);
            Assert.AreEqual(expectedFinalZ, minZ.Value, 1e-6, "Карман должен быть обработан до полной глубины");
        }

        // ------------------------------------------------------------------
        // Покрытие: кромка у стенки + центр
        // ------------------------------------------------------------------

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_CoversBoundaryAndCenter_Circle(PocketStrategy strategy)
        {
            var op = Configure(Circle(), strategy);
            var lines = Run(op);
            var toolRadius = ToolDiameter / 2.0;
            double expectedOuterRadius = 20.0 - toolRadius; // смещённый контур

            double maxR = 0, minR = double.MaxValue;
            foreach (var m in GCodeLineParser.LinearXyMoves(lines))
            {
                double r = Math.Sqrt(m.X.Value * m.X.Value + m.Y.Value * m.Y.Value);
                if (r > maxR) maxR = r;
                if (r < minR) minR = r;
            }

            // Кромка: инструмент доходит до смещённого контура (в пределах шага)
            Assert.IsTrue(maxR > expectedOuterRadius - Step,
                $"Максимальный радиус {maxR:F3} должен быть близок к контуру {expectedOuterRadius:F3}");
            // Центр: проходы проходят рядом с центром (в пределах шага)
            Assert.IsTrue(minR < Step, $"Минимальный радиус {minR:F3} должен быть мал (центр обработан)");
        }

        // ------------------------------------------------------------------
        // Отсутствие выхода за контур
        // ------------------------------------------------------------------

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        [DataRow(PocketStrategy.Lines)]
        public void EachStrategy_ToolCenterInsideContour_Circle(PocketStrategy strategy)
        {
            var op = Configure(Circle(), strategy);
            var lines = Run(op);
            var geometry = OperationCatalog.CreatePocketGeometry(op);
            double toolRadius = ToolDiameter / 2.0;

            int checked_ = 0;
            foreach (var m in GCodeLineParser.LinearXyMoves(lines))
            {
                // Только точки на рабочей Z (дно слоя) — фрезерные, не связочные подъёмы.
                if (m.Z.HasValue && Math.Abs(m.Z.Value - (-TotalDepth)) > 1e-6)
                    continue;
                Assert.IsTrue(geometry.IsPointInside(m.X.Value, m.Y.Value, toolRadius, 0.0),
                    $"Точка ({m.X.Value:F3},{m.Y.Value:F3}) вне смещённого контура");
                checked_++;
            }
            Assert.IsTrue(checked_ > 0, "Не проверено ни одной точки");
        }

        [TestMethod]
        public void EachStrategy_ToolCenterInsideContour_Rectangle()
        {
            foreach (var strategy in AllStrategies)
            {
                var op = Configure(Rectangle(), strategy);
                var lines = Run(op);
                var geometry = OperationCatalog.CreatePocketGeometry(op);
                double toolRadius = ToolDiameter / 2.0;

                foreach (var m in GCodeLineParser.LinearXyMoves(lines))
                {
                    if (m.Z.HasValue && Math.Abs(m.Z.Value - (-TotalDepth)) > 1e-6)
                        continue;
                    Assert.IsTrue(geometry.IsPointInside(m.X.Value, m.Y.Value, toolRadius, 0.0),
                        $"[{strategy}] Точка ({m.X.Value:F3},{m.Y.Value:F3}) вне смещённого контура");
                }
            }
        }

        // ------------------------------------------------------------------
        // Зигзаг: рез вдоль скан-линий
        // ------------------------------------------------------------------

        /// <summary>
        /// Каждая скан-линия зигзага проходится резом вдоль неё — от края
        /// сегмента до края, — а не пересекается диагональю. Прежде стратегия
        /// выводила только дальний конец сегмента: рез «x1 → x2», записанный
        /// в её же описании, не выполнялся никогда, траектория вырождалась
        /// в диагонали через весь карман, а у стенок шаг между реально
        /// пройденными путями удваивался. Инварианты покрытия дефекта
        /// не видели: радиус фрезы больше шага, и кромка инструмента
        /// «накрывала» пропущенное даже с диагоналей.
        /// </summary>
        [TestMethod]
        public void ZigZag_CutsAlongEachScanLine()
        {
            var op = Configure(Circle(), PocketStrategy.ZigZag);
            var lines = Run(op);

            var offsetRadius = 20.0 - ToolDiameter / 2.0;
            var cuts = new List<(double y, double length)>();
            (double x, double y)? previous = null;
            foreach (var m in GCodeLineParser.LinearXyMoves(lines))
            {
                var current = (x: m.X.Value, y: m.Y.Value);
                if (previous.HasValue
                    && Math.Abs(previous.Value.y - current.y) < 1e-6
                    && Math.Abs(previous.Value.x - current.x) > Step / 2)
                {
                    cuts.Add((current.y, Math.Abs(previous.Value.x - current.x)));
                }

                previous = current;
            }

            Assert.IsTrue(cuts.Count >= 5,
                $"резов вдоль скан-линий найдено {cuts.Count} — зигзаг обязан резать вдоль каждой линии, а не диагоналями");

            // Рез тянется на всю хорду смещённого круга на высоте линии.
            // Допуск: округление координат кадра до трёх знаков плюс хорды
            // тесселяции контура — сегмент строится по многоугольнику,
            // а хорда считается по идеальному кругу.
            foreach (var (y, length) in cuts)
            {
                var chord = 2.0 * Math.Sqrt(Math.Max(0.0, offsetRadius * offsetRadius - y * y));
                Assert.IsTrue(length >= chord - 0.01,
                    $"рез на Y={y:F3} длиной {length:F3} короче хорды {chord:F3}");
            }
        }

        // ------------------------------------------------------------------
        // Z-переходы
        // ------------------------------------------------------------------

        [TestMethod]
        public void Lines_RetractsToSafeZBeforeEachCut()
        {
            var op = Configure(Circle(), PocketStrategy.Lines);
            var lines = Run(op);
            var moves = GCodeLineParser.ParseMoves(lines);

            // Каждый рез Lines — независимый вход в слой: отвод на SafeZ
            // (G0 Z SafeZ) → подход в плоскости → быстрый ход до верха слоя
            // (G0 Z ContourHeight) → врезание рабочей подачей (G1 Z рабочая)
            // → рез G1 XY. Проверяем, что каждому врезанию предшествовал
            // отвод на SafeZ, а сам спуск в материал — рабочий, а не быстрый:
            // G0 на дно слоя означал бы удар торцом фрезы по материалу.
            int entries = 0, entriesAfterRetract = 0;
            bool sawRetract = false, sawLayerTop = false;
            foreach (var m in moves)
            {
                if (m.IsRapid && m.Z.HasValue)
                {
                    Assert.IsTrue(m.Z.Value >= ContourHeight - 1e-6,
                        $"Быстрый ход опускает инструмент на Z={m.Z.Value} — ниже верха слоя");

                    if (Math.Abs(m.Z.Value - SafeZ) < 1e-6)
                    {
                        sawRetract = true;
                        sawLayerTop = false;
                    }
                    else if (Math.Abs(m.Z.Value - ContourHeight) < 1e-6)
                    {
                        sawLayerTop = true;
                    }
                }
                else if (m.IsLinear && m.Z.HasValue && Math.Abs(m.Z.Value - (-TotalDepth)) < 1e-6)
                {
                    entries++;
                    if (sawRetract && sawLayerTop) entriesAfterRetract++;
                    sawRetract = false;
                    sawLayerTop = false;
                }
            }

            Assert.IsTrue(entries > 0, "Ожидались врезания в слой (резы)");
            Assert.AreEqual(entries, entriesAfterRetract,
                "Каждому врезанию должен предшествовать отвод на SafeZ и подход к верху слоя");
        }

        [TestMethod]
        [DataRow(PocketStrategy.Concentric)]
        [DataRow(PocketStrategy.Spiral)]
        [DataRow(PocketStrategy.Radial)]
        [DataRow(PocketStrategy.ZigZag)]
        public void NonLines_SingleZDescent_PerLayer(PocketStrategy strategy)
        {
            var op = Configure(Circle(), strategy);
            var lines = Run(op);
            var moves = GCodeLineParser.ParseMoves(lines);

            // Однослойный карман: ровно одно рабочее опускание G1 Z (в дно слоя).
            int g1Zdescents = moves.Count(m => m.IsLinear && m.Z.HasValue);
            Assert.AreEqual(1, g1Zdescents,
                $"Стратегия {strategy} не должна делать Z-движений внутри слоя (ожидалось 1 G1 Z, получено {g1Zdescents})");
        }

        // ------------------------------------------------------------------
        // Направление CW/CCW (Concentric)
        // ------------------------------------------------------------------

        [TestMethod]
        public void Concentric_CWAndCCW_ProduceSamePointSet()
        {
            var cw = Configure(Circle(), PocketStrategy.Concentric);
            cw.Direction = MillingDirection.Clockwise;
            var ccw = Configure(Circle(), PocketStrategy.Concentric);
            ccw.Direction = MillingDirection.CounterClockwise;

            var cwPoints = GCodeLineParser.LinearXyMoves(Run(cw))
                .Select(m => (Math.Round(m.X.Value, 3), Math.Round(m.Y.Value, 3)))
                .OrderBy(p => p.Item1).ThenBy(p => p.Item2)
                .ToList();
            var ccwPoints = GCodeLineParser.LinearXyMoves(Run(ccw))
                .Select(m => (Math.Round(m.X.Value, 3), Math.Round(m.Y.Value, 3)))
                .OrderBy(p => p.Item1).ThenBy(p => p.Item2)
                .ToList();

            CollectionAssert.AreEqual(cwPoints, ccwPoints,
                "CW и CCW должны давать один и тот же набор точек (в разном порядке)");
        }
    }
}
