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
        public void ReturnToStart_RapidMoveStaysAboveStock()
        {
            var lines = Generate(Circle(entryAngle: 3, safeDistance: 0.8));

            var horizontalRapids = lines
                .Select((line, index) => (line, index))
                .Where(item => item.line.StartsWith("G0 X", StringComparison.Ordinal))
                .ToList();

            Assert.IsTrue(horizontalRapids.Count > 1, "В программе есть возвраты между проходами");
            foreach (var rapid in horizontalRapids)
            {
                var previousZ = RapidZ(new[] { lines[rapid.index - 1] }).Single();
                Assert.IsTrue(previousZ >= 1.0 - 1e-9,
                    $"Перед {rapid.line} высота {previousZ} ниже безопасной высоты над заготовкой");
            }
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

            // Каждый виток заканчивается отводом над верхом заготовки.
            var retracts = RapidZ(lines).Count(z => Math.Abs(z - 1.0) < 1e-9);
            Assert.IsTrue(retracts >= 2,
                $"Ожидалось не менее двух отводов между витками, найдено {retracts}");
        }

        [TestMethod]
        public void LargeSafeDistance_CanRaiseClearanceAboveSafeHeight()
        {
            var lines = Generate(Circle(entryAngle: 3, safeDistance: 5));

            Assert.IsTrue(RapidZ(lines).Any(z => Math.Abs(z - 4.0) < 1e-9),
                "Явно больший зазор между проходами сохраняет своё назначение");
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

        private static ProfileRectangleOperation Rectangle(double entryAngle)
            => new ProfileRectangleOperation
            {
                Name = "Rectangle",
                Width = 40,
                Height = 20,
                ReferencePointX = 0,
                ReferencePointY = 0,
                ReferencePointType = ReferencePointType.Center,
                TotalDepth = 1,
                StepDepth = 1,
                ToolDiameter = 3,
                EntryMode = EntryMode.Angled,
                EntryAngle = entryAngle,
                SafeDistanceBetweenPasses = 0.8
            };

        /// <summary>Наклонные рабочие сегменты траектории: рампа входа.</summary>
        private static List<((double x, double y, double z) From, (double x, double y, double z) To)> RampSegments(
            OperationBase operation)
        {
            var toolPath = new SimpleGCodeGenerator()
                .BuildToolPath(new List<OperationBase> { operation }, new GCodeSettings());

            var segments = new List<((double, double, double), (double, double, double))>();
            var position = (x: 0.0, y: 0.0, z: 0.0);
            foreach (var move in toolPath.Moves())
            {
                var target = (x: move.X ?? position.x, y: move.Y ?? position.y, z: move.Z ?? position.z);
                var movesInPlane = Math.Abs(target.x - position.x) > 1e-9 || Math.Abs(target.y - position.y) > 1e-9;
                var movesInDepth = Math.Abs(target.z - position.z) > 1e-9;
                if (move.Kind == Toolpath.ToolMoveKind.Linear && movesInPlane && movesInDepth)
                    segments.Add((position, target));
                position = target;
            }

            return segments;
        }

        /// <summary>Расстояние точки до замкнутой ломаной.</summary>
        private static double DistanceToPolyline(double x, double y, IReadOnlyList<(double x, double y)> points)
        {
            var best = double.MaxValue;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var d = Geometry.Geometry2D.DistanceToSegment(
                    x, y, points[i].x, points[i].y, points[i + 1].x, points[i + 1].y,
                    Geometry.GeometryTolerances.Degenerate);
                if (d < best) best = d;
            }

            return best;
        }

        /// <summary>
        /// Рампа не срезает углы: каждый её сегмент лежит на контуре целиком.
        /// Прежде точки рампы брались с контура через равные доли пути,
        /// вершина угла попадала между ними, и хорда шла напрямик через
        /// угол — зарез детали, который не исправить следующим проходом.
        /// Середина сегмента-хорды лежала бы в стороне от контура.
        /// </summary>
        [TestMethod]
        public void Ramp_DoesNotCutCorners()
        {
            var operation = Rectangle(entryAngle: 1);
            var contour = new GCodeGenerators.Geometry.RectangleProfileGeometry(operation)
                .GetContourPoints(0, operation.Direction).ToList();

            var ramp = RampSegments(operation);
            Assert.IsTrue(ramp.Count > 0, "Рампа построена");

            foreach (var (from, to) in ramp)
            {
                var midX = (from.x + to.x) / 2;
                var midY = (from.y + to.y) / 2;
                Assert.IsTrue(DistanceToPolyline(midX, midY, contour) < 1e-6,
                    $"Сегмент рампы ({from.x:0.###};{from.y:0.###})→({to.x:0.###};{to.y:0.###}) сошёл с контура");
            }
        }

        /// <summary>
        /// Рампа проходит вершины контура точно: изломы — это точки её
        /// траектории, а не препятствия между сэмплами. Рампа на градусном
        /// угле идёт витками почти в полный периметр, поэтому каждая из
        /// четырёх вершин прямоугольника обязана встретиться среди концов
        /// её сегментов.
        /// </summary>
        [TestMethod]
        public void Ramp_PassesEveryCornerExactly()
        {
            var operation = Rectangle(entryAngle: 1);
            // Угол подбирается из глубины витка: рампа проходит девять
            // десятых периметра одним витком, и все четыре вершины
            // прямоугольника лежат на её пути при любом умолчании отвода.
            var perimeter = 2 * (operation.Width + operation.Height);
            var rampDepth = operation.StepDepth + operation.RetractHeight;
            operation.EntryAngle = Math.Atan(rampDepth / (perimeter * 0.9)) * 180 / Math.PI;

            var segments = RampSegments(operation);
            // Стартовая вершина — начало рампы, остальные обязаны быть
            // концами её сегментов: обе стороны излома принадлежат рампе.
            var points = segments.Select(segment => segment.From)
                .Concat(segments.Select(segment => segment.To))
                .ToList();

            foreach (var (cornerX, cornerY) in new[] { (-20.0, -10.0), (20.0, -10.0), (20.0, 10.0), (-20.0, 10.0) })
            {
                Assert.IsTrue(
                    points.Any(p => Math.Abs(p.x - cornerX) < 1e-9 && Math.Abs(p.y - cornerY) < 1e-9),
                    $"Вершина ({cornerX};{cornerY}) не пройдена рампой");
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
