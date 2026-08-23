using System;
using GCodeGenerator.GCodeGenerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Юнит-тесты ContourCutoffAnalyzer (пункт 4.6 плана): эвристики отсечки
    /// слоёв DXF-кармана как чистого класса — площади, «песочные часы»,
    /// смена обхода, смена векторов, размер контура.
    ///
    /// Формула «песочных часов»: по площадям первых двух слоёв
    /// ratio = A2/A1, номер слоя, где An = A0 * ratio^(n-1) <= 0.01 * A0:
    /// n = ceil(log(0.01)/log(ratio) + 1), минимум 2.
    /// Для ratio = 0.5 → n = ceil(6.644 + 1) = 8.
    /// </summary>
    [TestClass]
    public class ContourCutoffAnalyzerTests
    {
        private const double Taper0 = 0.0;
        private const double Taper5 = 5.0;

        private static bool Skip(
            ContourCutoffAnalyzer a,
            int contour = 0,
            double area = 100,
            int pass = 1,
            double taper = Taper0,
            bool winding = false,
            bool vector = false,
            bool tooSmall = false)
        {
            return a.ShouldSkip(contour, area, pass, taper, winding, vector, tooSmall);
        }

        /// <summary>Обрабатывает слой: ShouldSkip + RecordMilled (если не пропущен).</summary>
        private static bool Mill(ContourCutoffAnalyzer a, int contour, double area, int pass, double taper = Taper0)
        {
            bool skip = Skip(a, contour, area, pass, taper);
            if (!skip)
                a.RecordMilled(contour, area);
            return skip;
        }

        [TestMethod]
        public void Pass1_AlwaysProcessed()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Skip(a, area: 100, pass: 1));
            Assert.IsFalse(Skip(a, area: 0, pass: 1));
        }

        [TestMethod]
        public void Pass2_RatioHalf_HourglassLayer8_NotReached()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsFalse(Mill(a, 0, 50, 2)); // ratio 0.5 → «песочные часы» на слое 8
        }

        [TestMethod]
        public void Hourglass_SequenceHalving_StopsAtPass8()
        {
            var a = new ContourCutoffAnalyzer();
            double area = 100;
            for (int pass = 1; pass <= 7; pass++)
            {
                area /= 2; // 50, 25, 12.5, ...
                Assert.IsFalse(Mill(a, 0, area, pass), $"Слой {pass} не должен прерываться");
            }
            Assert.IsTrue(Mill(a, 0, area / 2, 8), "Слой 8 — точка «песочных часов»");
        }

        [TestMethod]
        public void Hourglass_Ratio001_LayerClampedTo2_StopsAtPass2()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 0.1, pass: 2), "ratio 0.001 → слой 2");
        }

        [TestMethod]
        public void Hourglass_Ratio001_Exact_StopsAtPass2()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 1.0, pass: 2), "ratio 0.01 → n = 2");
        }

        [TestMethod]
        public void RatioOne_TaperZero_NoHourglass_NoStop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsFalse(Skip(a, area: 100, pass: 2), "ratio 1.0 — «песочных часов» нет");
        }

        [TestMethod]
        public void TaperZero_AreaIncreased_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 100.00001, pass: 2), "уклон 0: рост площади — ошибка");
        }

        [TestMethod]
        public void TaperZero_AreaDecreased_Ok()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsFalse(Skip(a, area: 50, pass: 2));
        }

        [TestMethod]
        public void TaperPositive_AreaEqual_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1, Taper5));
            Assert.IsTrue(Skip(a, area: 100, pass: 2, taper: Taper5), "уклон > 0: площадь не уменьшилась — инверсия");
        }

        [TestMethod]
        public void TaperPositive_AreaIncreased_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1, Taper5));
            Assert.IsTrue(Skip(a, area: 101, pass: 2, taper: Taper5));
        }

        [TestMethod]
        public void TaperPositive_AreaDecreased_Ok()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1, Taper5));
            Assert.IsFalse(Skip(a, area: 50, pass: 2, taper: Taper5));
        }

        [TestMethod]
        public void WindingChanged_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 50, pass: 2, winding: true), "критерий 2: смена обхода");
        }

        [TestMethod]
        public void VectorChanged_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 50, pass: 2, vector: true), "критерий 3: смена вектора");
        }

        [TestMethod]
        public void ContourTooSmall_Stop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsTrue(Skip(a, area: 50, pass: 2, tooSmall: true));
        }

        [TestMethod]
        public void Criterion1_RequiresRecordMilled()
        {
            var a = new ContourCutoffAnalyzer();
            // Слой 1 обработан, но RecordMilled не вызван — критерий 1 не применяется
            Assert.IsFalse(Skip(a, area: 100, pass: 1));
            Assert.IsFalse(Skip(a, area: 1000, pass: 2), "без RecordMilled сравнение площади невозможно");
        }

        [TestMethod]
        public void RecordMissingContour_Pass1_Then_Pass2_ZeroFirstArea()
        {
            var a = new ContourCutoffAnalyzer();
            a.RecordMissingContour(0, 1);
            Assert.IsFalse(Skip(a, area: 50, pass: 2), "firstArea = 0 → «песочных часов» нет");
        }

        [TestMethod]
        public void RecordMissingContour_Pass3_DoesNotInitialize()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Skip(a, area: 100, pass: 1));
            a.RecordMilled(0, 100);
            a.RecordMissingContour(0, 2); // данные уже есть — не перезаписываются
            Assert.IsFalse(Skip(a, area: 50, pass: 3));
        }

        [TestMethod]
        public void MultipleContours_IndependentState()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 100, 1));
            Assert.IsFalse(Mill(a, 1, 100, 1));
            Assert.IsFalse(Mill(a, 0, 50, 2), "контур 0: площадь уменьшилась — ок");
            Assert.IsTrue(Skip(a, 1, 101, 2), "контур 1: рост площади — стоп (состояние независимое)");
        }

        [TestMethod]
        public void ZeroAreas_NoStop()
        {
            var a = new ContourCutoffAnalyzer();
            Assert.IsFalse(Mill(a, 0, 0, 1));
            Assert.IsFalse(Skip(a, area: 0, pass: 2));
        }

        [TestMethod]
        public void MissingContours_TwoPasses_ThenArea()
        {
            var a = new ContourCutoffAnalyzer();
            a.RecordMissingContour(0, 1);
            a.RecordMissingContour(0, 2); // данные уже инициализированы нулями
            Assert.IsFalse(Skip(a, area: 50, pass: 3), "нет предыдущей площади и «песочных часов»");
        }
    }
}
