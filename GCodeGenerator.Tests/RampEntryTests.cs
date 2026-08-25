using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Наклонное врезание и безопасное расстояние между проходами.
    ///
    /// Параметр показывался в окнах контуров и сохранялся в проект, но
    /// генератор его не читал: его не было даже в интерфейсе операции
    /// профиля, через который работает генератор. Название обещало влияние
    /// на траекторию, а инструмент возвращался к началу контура через
    /// безопасную высоту — то есть выше и дольше, чем просил пользователь.
    /// </summary>
    [TestClass]
    public class RampEntryTests
    {
        private static ProfileCircleOperation Circle(double entryAngle, double safeDistance)
            => new ProfileCircleOperation
            {
                Name = "Circle",
                CenterX = 20,
                CenterY = 20,
                Radius = 10,
                TotalDepth = 2,
                StepDepth = 1,
                ToolDiameter = 3,
                EntryMode = EntryMode.Angled,
                EntryAngle = entryAngle,
                SafeDistanceBetweenPasses = safeDistance
            };

        private static List<string> Generate(OperationBase operation)
        {
            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = false;
            return new SimpleGCodeGenerator()
                .Generate(new List<OperationBase> { operation }, settings)
                .Lines.ToList();
        }

        /// <summary>Высоты Z всех холостых перемещений программы.</summary>
        private static List<double> RapidZ(IEnumerable<string> lines)
            => lines
                .Where(line => line.StartsWith("G0 Z", StringComparison.Ordinal))
                .Select(line => double.Parse(
                    line.Substring(4, line.IndexOf(' ', 4) - 4), CultureInfo.InvariantCulture))
                .ToList();

        [TestMethod]
        public void ReturnToStart_RetractsBySafeDistanceAboveMaterial()
        {
            var lines = Generate(Circle(entryAngle: 3, safeDistance: 0.8));

            // Слой заканчивается на Z-1; отвод перед рабочим проходом — на
            // 0,8 мм выше него, а не на безопасной высоте 1 мм над заготовкой.
            Assert.IsTrue(RapidZ(lines).Any(z => Math.Abs(z - (-0.2)) < 1e-9),
                "Отвод на 0,8 мм над только что пройденной глубиной");
        }

        [TestMethod]
        public void ZeroSafeDistance_KeepsPreviousBehaviour()
        {
            var lines = Generate(Circle(entryAngle: 3, safeDistance: 0));

            Assert.IsFalse(RapidZ(lines).Any(z => Math.Abs(z - (-0.2)) < 1e-9),
                "Без заданного расстояния отвода над материалом быть не должно");
            Assert.IsTrue(RapidZ(lines).Any(z => Math.Abs(z - 1.0) < 1e-9),
                "Возврат к началу контура идёт через безопасную высоту, как раньше");
        }

        /// <summary>
        /// Малый угол на всю глубину слоя не укладывается в один оборот:
        /// спуск идёт несколькими витками, между которыми инструмент уходит
        /// от материала. Прежде угол молча становился круче заданного.
        /// </summary>
        [TestMethod]
        public void ShallowAngle_DescendsInSeveralLaps()
        {
            var lines = Generate(Circle(entryAngle: 1, safeDistance: 0.8));

            // Каждый виток заканчивается отводом на 0,8 мм над своей глубиной.
            var retracts = RapidZ(lines).Count(z => Math.Abs(z - 0.45) < 1e-9 || Math.Abs(z - (-0.2)) < 1e-9);
            Assert.IsTrue(retracts >= 2,
                $"Ожидалось не менее двух отводов между витками, найдено {retracts}");
        }

        [TestMethod]
        public void SteepAngle_FitsIntoSingleLap()
        {
            var shallow = Generate(Circle(entryAngle: 1, safeDistance: 0.8));
            var steep = Generate(Circle(entryAngle: 30, safeDistance: 0.8));

            Assert.IsTrue(steep.Count < shallow.Count,
                "Крутая рампа укладывается в один виток и даёт более короткую программу");
        }

        /// <summary>
        /// Рампа опускает инструмент монотонно: подъёмов внутри рабочего хода
        /// быть не должно — это резание вверх по уже снятому материалу.
        /// Операция взята однослойной, чтобы переход к следующему слою не
        /// считался подъёмом рампы.
        /// </summary>
        [TestMethod]
        public void RampDescendsMonotonically()
        {
            var singleLayer = Circle(entryAngle: 1, safeDistance: 0.8);
            singleLayer.TotalDepth = 1;
            singleLayer.StepDepth = 1;
            var lines = Generate(singleLayer);

            double? previous = null;
            foreach (var line in lines.Where(l => l.StartsWith("G1 X", StringComparison.Ordinal) && l.Contains(" Z")))
            {
                var zPart = line.Substring(line.IndexOf(" Z", StringComparison.Ordinal) + 2);
                var z = double.Parse(zPart.Substring(0, zPart.IndexOf(' ')), CultureInfo.InvariantCulture);
                if (previous.HasValue)
                    Assert.IsTrue(z <= previous.Value + 1e-9, $"Рампа поднялась с {previous} до {z}");
                previous = z;
            }
        }

        /// <summary>
        /// Вертикальное врезание безопасным расстоянием не пользуется:
        /// параметр относится только к рампе.
        /// </summary>
        [TestMethod]
        public void VerticalEntry_IsUnaffected()
        {
            var vertical = Circle(entryAngle: 3, safeDistance: 0.8);
            vertical.EntryMode = EntryMode.Vertical;
            var withoutDistance = Circle(entryAngle: 3, safeDistance: 0);
            withoutDistance.EntryMode = EntryMode.Vertical;

            CollectionAssert.AreEqual(Generate(vertical), Generate(withoutDistance),
                "Вертикальный вход не зависит от расстояния между проходами");
        }
    }
}
