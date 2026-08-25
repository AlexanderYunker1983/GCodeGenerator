using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.GCodeGenerators.Helpers;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Характеризационные тесты рискованной логики (пункт 0.5 плана).
    ///
    /// Задача — зафиксировать ТЕКУЩЕЕ поведение рискованных участков, чтобы
    /// последующие фазы (4/5) могли менять его осознанно:
    /// - DXF-карман: остановка обработки, когда эквидистанта перестаёт давать
    ///   области, распад области на части («песочные часы»), крайние случаи
    ///   (узкий контур с уклоном, контур меньше фрезы);
    /// - спираль при малых шагах и малом контуре;
    /// - уклон стенок (taper) на карманах;
    /// - дуги G2/G3 и fallback на полилинии при AllowArcs=false.
    ///
    /// Примечания:
    /// - Тесты вызывают генераторы напрямую (UnifiedPocketGenerator / UnifiedProfileGenerator
    ///   с List&lt;string&gt;-коллектором) — это уровень, на котором живёт тестируемая логика.
    /// - Ранее зафиксированные T9/T4 и Geo_Square защищают исправление «фантомной
    ///   фрезеровки»: контур меньше фрезы не даёт областей эквидистанты и
    ///   не порождает ни одного рабочего перемещения.
    /// - Helpers_CalculateStep фиксирует оставшийся guard `step &lt; 1e-6` — no-op
    ///   (переприсваивает то же значение).
    /// - У профилей НЕТ параметра taper (в плане пункт упоминает «taper на профилях и карманах»,
    ///   но в моделях профилей/`IProfileOperation` WallTaperAngleDeg отсутствует — только у карманов).
    ///   Поэтому taper покрывается только для карманов.
    /// - Тесты «песочных часов» и П-образного контура фиксируют поведение после
    ///   перехода на корректное смещение контура: область, распавшаяся на части,
    ///   фрезеруется по частям, а обработка останавливается тогда, когда частей
    ///   не остаётся. До перехода те же случаи давали самопересекающуюся
    ///   эквидистанту, и их приходилось распознавать эвристиками площади,
    ///   направления обхода и «песочных часов».
    /// - Культура прогона не закрепляется: вывод инвариантен по построению,
    ///   как в GoldenTests.
    /// </summary>
    [TestClass]
    public class RiskyLogicTests
    {
        // ------------------------------------------------------------------
        // Вспомогательные методы
        // ------------------------------------------------------------------

        private static List<string> RunPocket(OperationBase op, bool comments = true)
            => RunGenerator(new UnifiedPocketGenerator(), op, new GCodeSettings { Format = new GCodeFormatSettings { UseComments = comments } });

        private static List<string> RunProfile(OperationBase op, GCodeSettings settings)
            => RunGenerator(new UnifiedProfileGenerator(), op, settings);

        /// <summary>Запуск операционного генератора: траектория плюс постпроцессор.</summary>
        private static List<string> RunGenerator(IOperationGenerator generator, OperationBase op, GCodeSettings settings)
        {
            var toolPath = Fixtures.OperationToolPath.Build(generator, op, settings);
            // Прямой вызов генератора (без фрейма): без линейных номеров, как до порта.
            var renderSettings = new GCodeSettings
            {
                Format = new GCodeFormatSettings
                {
                    UseLineNumbers = false,
                    UseComments = settings.Format.UseComments,
                    UsePaddedGCodes = settings.Format.UsePaddedGCodes,
                },
            };
            return new GenericPostProcessor().Build(toolPath, renderSettings).Lines.ToList();
        }

        private static Polyline2D Poly(params (double x, double y)[] pts)
        {
            var p = new Polyline2D();
            foreach (var pt in pts)
                p.Points.Add(new Point2D { X = pt.x, Y = pt.y });
            return p;
        }

        private static int CountPasses(List<string> lines) =>
            lines.Count(l => l.StartsWith("(Pass ", StringComparison.Ordinal));

        private static bool HasStopComment(List<string> lines) =>
            lines.Any(l => l.Contains("(Contour too small for tool, stopping)"));

        private static int CountG1XY(List<string> lines) =>
            lines.Count(l => l.Contains("G1 X") && l.Contains("Y"));

        private static int CountArcs(List<string> lines, string gcode) =>
            lines.Count(l => l.Contains(gcode + " "));

        private static double MaxDistFromCenter(List<string> lines, double cx, double cy)
        {
            var re = new Regex(@"G1\s+X(-?[\d.]+)\s+Y(-?[\d.]+)");
            double maxD = 0;
            foreach (var l in lines)
            {
                var m = re.Match(l);
                if (!m.Success)
                    continue;
                var x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var y = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                var d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > maxD) maxD = d;
            }
            return maxD;
        }

        /// <summary>Количество G1 XY-перемещений с координатами в прямоугольной области.</summary>
        private static int CountG1XYInRegion(List<string> lines, double xMin, double xMax, double yMin, double yMax)
        {
            var re = new Regex(@"G1\s+X(-?[\d.]+)\s+Y(-?[\d.]+)");
            int count = 0;
            foreach (var l in lines)
            {
                var m = re.Match(l);
                if (!m.Success)
                    continue;
                var x = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                var y = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                if (x >= xMin && x <= xMax && y >= yMin && y <= yMax)
                    count++;
            }
            return count;
        }

        // ------------------------------------------------------------------
        // Уклон стенок (taper) и отсечка — не-DXF карманы (круг/прямоугольник)
        // ------------------------------------------------------------------

        /// <summary>
        /// T1: круг R=6, фреза 3, taper 45°, глубина 10, шаг 1.
        /// Эффективный радиус слоя n: r_eff = 6 − 1.5 − (n−1) = 4.5 − (n−1).
        /// Слои 1–4 фрезеруются; на 5-м r_eff = −0.5 → CirclePocketGeometry.IsContourTooSmall
        /// (эффективный диаметр &lt; 5% диаметра фрезы) → остановка.
        /// </summary>
        [TestMethod]
        public void Taper_Circle_StopsBeforeFullDepth()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 6,
                ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45
            };
            var lines = RunPocket(op);
            Assert.AreEqual(5, CountPasses(lines), "5 комментариев (Pass N): 4 фрезерных слоя + 1 остановка");
            Assert.IsTrue(HasStopComment(lines), "Ожидался комментарий об остановке");
            Assert.AreEqual(1012, CountG1XY(lines), "Число G1 XY-перемещений");
        }

        /// <summary>
        /// T2: прямоугольник 7.5×7.5 (ReferencePoint 0,0), фреза 3, taper 45°, глубина 10.
        /// Остановка на 3-м слое по критерию 1 (рост площади): bowtie-артефакт оффсета
        /// даёт площади 6.25 → 0.25 → 2.25 (рост ⇒ «инверсия или вырождение»).
        /// </summary>
        [TestMethod]
        public void Taper_Rectangle_StopsAtPass3_ByAreaIncrease()
        {
            var op = new PocketRectangleOperation
            {
                Width = 7.5, Height = 7.5, ReferencePointX = 0, ReferencePointY = 0,
                ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45
            };
            var lines = RunPocket(op);
            Assert.AreEqual(3, CountPasses(lines));
            Assert.IsTrue(HasStopComment(lines));
            Assert.AreEqual(203, CountG1XY(lines));
        }

        /// <summary>
        /// T5: taper 0 — контур не сужается, карман фрезеруется на полную глубину.
        /// </summary>
        [TestMethod]
        public void Taper_Zero_MillsFullDepth()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 50,
                ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0
            };
            var lines = RunPocket(op);
            Assert.AreEqual(4, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(23152, CountG1XY(lines));
        }

        /// <summary>
        /// T6: круг R=1.6, фреза 3 — эффективный диаметр 0.2 БОЛЬШЕ порога 0.15
        /// (5% диаметра фрезы) → фрезеруется полностью, без остановки.
        /// </summary>
        [TestMethod]
        public void Circle_EffectiveDiameter_AboveThreshold_Milled()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 1.6,
                ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, WallTaperAngleDeg = 0
            };
            var lines = RunPocket(op);
            Assert.AreEqual(2, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(94, CountG1XY(lines));
        }

        /// <summary>
        /// T8: круг R=1.55, фреза 3 — эффективный диаметр 0.1 МЕНЬШЕ порога 0.15
        /// → остановка уже на 1-м слое, перемещений по XY нет вообще.
        /// Граничная пара с T6 (порог 5% диаметра фрезы).
        /// </summary>
        [TestMethod]
        public void Circle_EffectiveDiameter_BelowThreshold_StopsWithoutMoves()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 1.55,
                ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, WallTaperAngleDeg = 0
            };
            var lines = RunPocket(op);
            Assert.AreEqual(1, CountPasses(lines));
            Assert.IsTrue(HasStopComment(lines));
            Assert.AreEqual(0, CountG1XY(lines), "Ни одного G1 XY-перемещения");
        }

        // ------------------------------------------------------------------
        // DXF-карман: отсечка слоя, «песочные часы», крайние случаи
        // ------------------------------------------------------------------

        /// <summary>
        /// T3: узкая трапеция (0,0)-(12,0)-(8,6)-(4,6), фреза 3, taper 10°, глубина 5.
        /// Все 5 слоёв фрезеруются, остановки нет.
        /// </summary>
        [TestMethod]
        public void Dxf_Trapezoid_Taper10_MillsFullDepth()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 5, StepDepth = 1, WallTaperAngleDeg = 10 };
            op.ClosedContours.Add(Poly((0, 0), (12, 0), (8, 6), (4, 6), (0, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(5, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(791, CountG1XY(lines));
        }

        /// <summary>
        /// T4: два контура — большой 40×20 и крошечный 2×2 (в (60,0)), фреза 3, глубина 4.
        /// Большой контур фрезеруется на полную глубину (4 слоя).
        /// Крошечный контур меньше фрезы пропускается, большой обрабатывается на полную глубину.
        /// </summary>
        [TestMethod]
        public void Dxf_MultiContour_SmallContourIsSkipped()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0 };
            op.ClosedContours.Add(Poly((0, 0), (40, 0), (40, 20), (0, 20), (0, 0)));
            op.ClosedContours.Add(Poly((60, 0), (62, 0), (62, 2), (60, 2), (60, 0)));
            var lines = RunPocket(op);

            Assert.AreEqual(4, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(5996, CountG1XY(lines));

            // T4b: разбивка перемещений по областям контуров.
            Assert.AreEqual(5996, CountG1XYInRegion(lines, -2, 42, -2, 22), "Перемещения в области большого контура");
            Assert.AreEqual(0, CountG1XYInRegion(lines, 58, 64, -2, 4),
                "Контур меньше диаметра фрезы не должен порождать траекторию");
        }

        /// <summary>
        /// T9: только крошечный квадрат 2×2, фреза 3, глубина 4.
        /// Контур меньше фрезы отсекается до построения оффсета: bowtie-артефакт
        /// не должен порождать ни одного рабочего XY-перемещения.
        /// </summary>
        [TestMethod]
        public void Dxf_TinyContour_IsSkippedBeforePhantomMilling()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 4, StepDepth = 1, WallTaperAngleDeg = 0 };
            op.ClosedContours.Add(Poly((60, 0), (62, 0), (62, 2), (60, 2), (60, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(1, CountPasses(lines), "Обработка останавливается на первом слое");
            Assert.IsTrue(HasStopComment(lines), "Ожидалось сообщение об отсечке контура");
            Assert.AreEqual(0, CountG1XY(lines), "Фантомных перемещений быть не должно");
        }

        /// <summary>
        /// T7: DXF-квадрат 7.5×7.5, фреза 3, taper 45°, глубина 10.
        /// Остановка на 3-м слое (как T2, не-DXF-прямоугольник — тот же bowtie-артефакт).
        /// Максимальное расстояние точек от центра (3.75, 3.75) на момент остановки — 1.768.
        /// </summary>
        [TestMethod]
        public void Dxf_Square_Taper45_StopsAtPass3()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 10, StepDepth = 1, WallTaperAngleDeg = 45 };
            op.ClosedContours.Add(Poly((0, 0), (7.5, 0), (7.5, 7.5), (0, 7.5), (0, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(3, CountPasses(lines));
            Assert.IsTrue(HasStopComment(lines));
            Assert.AreEqual(201, CountG1XY(lines));
            Assert.AreEqual(1.768, MaxDistFromCenter(lines, 3.75, 3.75), 0.01);
        }

        /// <summary>
        /// HG1: «песочные часы» (0,0)-(10,0)-(6,4)-(10,8)-(0,8)-(4,4), фреза 3, taper 15°, глубина 6.
        /// Перемычка в середине шириной 2 мм уже фрезы, поэтому смещение на
        /// радиус разрывает область на верхнюю и нижнюю половины — обе
        /// фрезеруются, пока уклон не съедает их полностью.
        /// Прежняя реализация возвращала здесь самопересекающийся контур,
        /// и обработка обрывалась эвристикой на первом же слое.
        /// </summary>
        [TestMethod]
        public void Dxf_Hourglass_Taper15_MillsBothHalvesUntilTaperClosesThem()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 15 };
            op.ClosedContours.Add(Poly((0, 0), (10, 0), (6, 4), (10, 8), (0, 8), (4, 4), (0, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(3, CountPasses(lines));
            Assert.IsTrue(HasStopComment(lines), "С ростом глубины уклон закрывает обе половины");
            Assert.IsTrue(CountG1XY(lines) > 0, "Половины «песочных часов» должны фрезероваться");
            Assert.IsTrue(CountG1XYInRegion(lines, -1, 11, -1, 4) > 0, "Нижняя половина обрабатывается");
            Assert.IsTrue(CountG1XYInRegion(lines, -1, 11, 4, 9) > 0, "Верхняя половина обрабатывается");
        }

        /// <summary>
        /// HG2: тот же контур «песочные часы», но taper 0 → обе половины
        /// фрезеруются на полную глубину (6 слоёв). Прежняя реализация
        /// проходила здесь по самопересекающейся эквидистанте и давала вдвое
        /// меньше перемещений.
        /// </summary>
        [TestMethod]
        public void Dxf_Hourglass_TaperZero_MillsFullDepth()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 0 };
            op.ClosedContours.Add(Poly((0, 0), (10, 0), (6, 4), (10, 8), (0, 8), (4, 4), (0, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(6, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(1086, CountG1XY(lines));
            Assert.IsTrue(CountG1XYInRegion(lines, -1, 11, -1, 4) > 0, "Нижняя половина обрабатывается");
            Assert.IsTrue(CountG1XYInRegion(lines, -1, 11, 4, 9) > 0, "Верхняя половина обрабатывается");
        }

        /// <summary>
        /// HG3: П-образный контур (невыпуклый), фреза 3, taper 10°, глубина 6.
        /// Основание и ножки имеют толщину 4 мм, поэтому на третьем слое
        /// эффективный радиус 1.5 + 3·tg10° = 2.029 требует 4.06 мм — области
        /// не остаётся. Прежняя реализация делала на этой глубине ещё один
        /// проход по испорченной эквидистанте.
        /// </summary>
        [TestMethod]
        public void Dxf_UShape_Taper10_StopsWhenWallsCloseIn()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3, TotalDepth = 6, StepDepth = 1, WallTaperAngleDeg = 10 };
            op.ClosedContours.Add(Poly((0, 0), (12, 0), (12, 10), (8, 10), (8, 4), (4, 4), (4, 10), (0, 10), (0, 0)));
            var lines = RunPocket(op);
            Assert.AreEqual(3, CountPasses(lines));
            Assert.IsTrue(HasStopComment(lines));
            Assert.IsTrue(CountG1XY(lines) > 0);
        }

        // ------------------------------------------------------------------
        // Спираль: малые шаги, малый контур
        // ------------------------------------------------------------------

        /// <summary>
        /// S1: круг R=10, фреза 3, шаг 10% от фрезы (0.3), глубина 1.
        /// Один слой, 3738 G1 XY-перемещений (много витков спирали).
        /// Максимальное расстояние от центра 8.501 ≈ R − toolRadius (8.5):
        /// спираль ограничена смещённым контуром.
        /// </summary>
        [TestMethod]
        public void Spiral_SmallStep_ManyTurns_BoundedByOffsetContour()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 10,
                ToolDiameter = 3, TotalDepth = 1, StepDepth = 1, StepPercentOfTool = 10
            };
            var lines = RunPocket(op);
            Assert.AreEqual(1, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(3738, CountG1XY(lines));
            Assert.AreEqual(8.501, MaxDistFromCenter(lines, 0, 0), 0.01,
                "Спираль ограничена смещённым контуром (R − toolRadius = 8.5)");
        }

        /// <summary>
        /// S2: нулевой шаг выборки отклоняется, а не подменяется сорока
        /// процентами. Прежде генератор молча подставлял «разумное» значение,
        /// и карман выбирался с шагом, которого пользователь не задавал.
        /// </summary>
        [TestMethod]
        public void Spiral_StepPercentZero_IsRejected()
        {
            var operation = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 10,
                ToolDiameter = 3, TotalDepth = 1, StepDepth = 1, StepPercentOfTool = 0
            };

            // Через полный генератор пользователь получает названную причину…
            var error = Assert.Throws<GCodeGenerationValidationException>(
                () => new SimpleGCodeGenerator().Generate(
                    new List<OperationBase> { operation }, new GCodeSettings()));
            Assert.IsTrue(error.Failures.Any(f => f.Issues.Any(i => i.Property == "StepPercentOfTool")),
                "Названа причина: шаг выборки");

            // …а расчёт шага не подставляет ничего и сам по себе.
            Assert.Throws<ArgumentOutOfRangeException>(() => RunPocket(operation));
        }

        /// <summary>
        /// S3: малый контур — круг R=4, фреза 3, шаг 40%, глубина 1.
        /// Один слой, 303 G1 XY-перемещения, спираль ограничена: maxDist = 2.500 = R − toolRadius.
        /// </summary>
        [TestMethod]
        public void Spiral_SmallContour_BoundedByOffsetContour()
        {
            var op = new PocketCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 4,
                ToolDiameter = 3, TotalDepth = 1, StepDepth = 1, StepPercentOfTool = 40
            };
            var lines = RunPocket(op);
            Assert.AreEqual(1, CountPasses(lines));
            Assert.IsFalse(HasStopComment(lines));
            Assert.AreEqual(303, CountG1XY(lines));
            Assert.AreEqual(2.500, MaxDistFromCenter(lines, 0, 0), 0.01);
        }

        // ------------------------------------------------------------------
        // Критерии DxfPocketGeometry напрямую (единичные проверки)
        // ------------------------------------------------------------------

        /// <summary>
        /// Тонкий треугольник (0,0)-(20,0)-(10,2), центроид (10, 0.6667).
        /// При смещении 0.5 область ещё остаётся, при 1.5 — уже нет: высота
        /// треугольника всего 2 мм. Прежняя реализация на 1.5 возвращала
        /// вывернутый контур, и распознавать его приходилось эвристикой смены
        /// направления обхода.
        /// </summary>
        [TestMethod]
        public void DxfGeometry_ThinTriangle_LosesAreaBeyondHalfHeight()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3 };
            var geo = new DxfPocketGeometry(op, Poly((0, 0), (20, 0), (10, 2), (0, 0)));

            var center = geo.GetCenter();
            Assert.AreEqual(10.0, center.x, 1e-4);
            Assert.AreEqual(2.0 / 3.0, center.y, 1e-4);

            Assert.IsFalse(geo.IsContourTooSmall(0, 0.5));
            Assert.AreEqual(1, geo.GetOffsetParts(0, 0.5).Count);

            Assert.IsTrue(geo.IsContourTooSmall(0, 1.5), "Фреза D3 не помещается по высоте треугольника");
            Assert.AreEqual(0, geo.GetOffsetParts(0, 1.5).Count);
        }

        /// <summary>
        /// G3–G5: квадрат 10×10. На o=1 и o=4.9 контур допустим. На o=5.1
        /// диаметр 10.2 уже больше минимальной ширины квадрата, поэтому контур
        /// отсекается до построения вырожденного оффсета.
        /// </summary>
        [TestMethod]
        public void DxfGeometry_Square_RejectsOffsetBeyondInradius()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3 };
            var geo = new DxfPocketGeometry(op, Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)));

            foreach (var o in new[] { 1.0, 4.9 })
            {
                Assert.IsFalse(geo.IsContourTooSmall(0, o), $"IsContourTooSmall(o={o})");
                Assert.AreEqual(1, geo.GetOffsetParts(0, o).Count, $"Число областей при o={o}");
            }

            Assert.IsTrue(geo.IsContourTooSmall(0, 5.1), "Оффсет больше вписанного радиуса должен отсекаться");
        }

        /// <summary>
        /// Кеш эквидистанты хранит только последнее смещение: при чередовании
        /// смещений каждый вызов обязан возвращать контур своего смещения,
        /// а повторный вызов с прежним смещением — тот же результат.
        /// Квадрат 20×20: площадь эквидистанты равна (20-2o)².
        /// </summary>
        [TestMethod]
        public void DxfGeometry_OffsetCache_AlternatingOffsets_ReturnsOwnContour()
        {
            var op = new PocketDxfOperation { ToolDiameter = 3 };
            var geo = new DxfPocketGeometry(op, Poly((0, 0), (20, 0), (20, 20), (0, 20), (0, 0)));

            foreach (var o in new[] { 2.0, 5.0, 2.0, 5.0, 2.0 })
            {
                var area = geo.GetContour(0, o).GetArea();
                var expected = (20.0 - 2 * o) * (20.0 - 2 * o);
                Assert.AreEqual(expected, area, 1e-6, $"Площадь эквидистанты при o={o}");
            }

            // Точка (3, 10) лежит внутри контура при o=2 и снаружи при o=5.
            Assert.IsTrue(geo.IsPointInside(3, 10, 0, 2.0));
            Assert.IsFalse(geo.IsPointInside(3, 10, 0, 5.0));
            Assert.IsTrue(geo.IsPointInside(3, 10, 0, 2.0), "Повторный вызов с прежним смещением");
        }

        /// <summary>
        /// Повернутый узкий прямоугольник имеет большой осевой bounding box,
        /// но его истинная минимальная ширина 2 мм. Фреза D3 не помещается.
        /// </summary>
        [TestMethod]
        public void DxfGeometry_RotatedNarrowContour_UsesMinimumWidthInsteadOfBoundingBox()
        {
            double diagonal = Math.Sqrt(0.5);
            var op = new PocketDxfOperation { ToolDiameter = 3 };
            var geo = new DxfPocketGeometry(op, Poly(
                (9 * diagonal, 11 * diagonal),
                (11 * diagonal, 9 * diagonal),
                (-9 * diagonal, -11 * diagonal),
                (-11 * diagonal, -9 * diagonal),
                (9 * diagonal, 11 * diagonal)));

            Assert.IsTrue(geo.IsContourTooSmall(1.5, 0),
                "Минимальная ширина 2 мм меньше диаметра фрезы 3 мм");
        }

        // ------------------------------------------------------------------
        // Хелперы: CalculateStep / CalculateTaperOffset
        // ------------------------------------------------------------------

        /// <summary>
        /// H1: значения CalculateStep.
        /// (3,40)=1.2; (3,0)=1.2 — fallback на 40% при stepPercent&lt;=0; (3,10)=0.3.
        /// ⚠️ Guard `if (step &lt; 1e-6) step = toolDiameter * 0.4` — no-op:
        /// переприсваивает то же самое значение (step уже равен toolDiameter*0.4).
        /// (1e-7,40)=4E-08 — guard не срабатывает по смыслу.
        /// </summary>
        [TestMethod]
        public void Helpers_CalculateStep_Values()
        {
            Assert.AreEqual(1.2, GCodeGenerationHelper.CalculateStep(3, 40), 1e-9);
            Assert.AreEqual(0.3, GCodeGenerationHelper.CalculateStep(3, 10), 1e-9);
            Assert.AreEqual(4e-08, GCodeGenerationHelper.CalculateStep(1e-7, 40), 1e-12,
                "Шаг считается и для крошечных диаметров");

            // Неположительный процент — отказ, а не «разумное» значение
            // вместо заданного: шаг определяет всю траекторию выборки.
            Assert.Throws<ArgumentOutOfRangeException>(() => GCodeGenerationHelper.CalculateStep(3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GCodeGenerationHelper.CalculateStep(3, -5));
        }

        /// <summary>
        /// H2: значения CalculateTaperOffset = depthFromTop * tan(angle).
        /// (2,0)=0; (2,45)=2; (1,10)≈0.176327.
        /// </summary>
        [TestMethod]
        public void Helpers_CalculateTaperOffset_Values()
        {
            Assert.AreEqual(0.0, GCodeGenerationHelper.CalculateTaperOffset(2, 0), 1e-12);
            Assert.AreEqual(2.0, GCodeGenerationHelper.CalculateTaperOffset(2, 45), 1e-9);
            Assert.AreEqual(0.176327, GCodeGenerationHelper.CalculateTaperOffset(1, 10), 1e-5);
        }

        // ------------------------------------------------------------------
        // Профили: дуги G2/G3 и fallback на полилинии
        // ------------------------------------------------------------------

        /// <summary>
        /// P1: круг R=10, MaxSegmentLength=0.5, AllowArcs=false, глубина 2 (2 прохода).
        /// Fallback на полилинию: 254 G1 XY-перемещений = 2 прохода × 127 точек.
        /// Формула: numSegments = max(8, ceil(2πR / MaxSegmentLength)) = 126 → 127 точек
        /// (включая замыкающую). Дуг G2/G3 в выводе нет.
        /// </summary>
        [TestMethod]
        public void Profile_ArcsOff_PolylinePointCountFormula()
        {
            var op = new ProfileCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 10,
                ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, MaxSegmentLength = 0.5
            };
            var lines = RunProfile(op, new GCodeSettings { Format = new GCodeFormatSettings { AllowArcs = false, UseComments = false } });
            Assert.AreEqual(254, CountG1XY(lines), "2 прохода × 127 точек");
            Assert.AreEqual(0, CountArcs(lines, "G2"), "Дуг G2 быть не должно");
            Assert.AreEqual(0, CountArcs(lines, "G3"), "Дуг G3 быть не должно");
            Assert.AreEqual(10.001, MaxDistFromCenter(lines, 0, 0), 0.01);
        }

        /// <summary>
        /// P2: тот же круг, MaxSegmentLength=2 → numSegments = ceil(2π·10/2) = 32 → 33 точки.
        /// 66 G1 XY-перемещений = 2 прохода × 33 точки.
        /// </summary>
        [TestMethod]
        public void Profile_ArcsOff_LargerSegment_FewerPoints()
        {
            var op = new ProfileCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 10,
                ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, MaxSegmentLength = 2
            };
            var lines = RunProfile(op, new GCodeSettings { Format = new GCodeFormatSettings { AllowArcs = false, UseComments = false } });
            Assert.AreEqual(66, CountG1XY(lines), "2 прохода × 33 точки");
            Assert.AreEqual(0, CountArcs(lines, "G2"));
            Assert.AreEqual(0, CountArcs(lines, "G3"));
            Assert.AreEqual(10.000, MaxDistFromCenter(lines, 0, 0), 0.01);
        }

        /// <summary>
        /// P3: круг R=10, AllowArcs=true, глубина 2 (2 прохода).
        /// 4 дуги G2 (2 полукруга на проход × 2 прохода), 0 G1 XY.
        /// Формат: I/J = центр − начальная точка; Clockwise → G2.
        /// Старт (10,0) → середина (−10,0): `G2 X-10.000 Y0.000 I-10.000 J0.000 F300.000`;
        /// середина → старт: `G2 X10.000 Y0.000 I10.000 J0.000 F300.000`.
        /// </summary>
        [TestMethod]
        public void Profile_ArcsOn_Circle_TwoSemicircleG2PerPass()
        {
            var op = new ProfileCircleOperation
            {
                CenterX = 0, CenterY = 0, Radius = 10,
                ToolDiameter = 3, TotalDepth = 2, StepDepth = 1, MaxSegmentLength = 0.5
            };
            var lines = RunProfile(op, new GCodeSettings { Format = new GCodeFormatSettings { AllowArcs = true, UseComments = false } });

            Assert.AreEqual(4, CountArcs(lines, "G2"), "4 дуги G2 (2 полукруга × 2 прохода)");
            Assert.AreEqual(0, CountArcs(lines, "G3"));
            Assert.AreEqual(0, CountG1XY(lines), "Линейных G1 XY-перемещений быть не должно");

            var g2Lines = lines.Where(l => l.Contains("G2 ")).ToList();
            Assert.AreEqual("G2 X-10.000 Y0.000 I-10.000 J0.000 F300.000", g2Lines[0]);
            Assert.AreEqual("G2 X10.000 Y0.000 I10.000 J0.000 F300.000", g2Lines[1]);
            // Второй проход повторяет тот же паттерн.
            Assert.AreEqual(g2Lines[0], g2Lines[2]);
            Assert.AreEqual(g2Lines[1], g2Lines[3]);
        }
    }
}
