using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Расчёт отверстий по шаблону сверления.
    ///
    /// Раньше формулы жили в девяти view-моделях диалогов, а тесты фикстур
    /// повторяли их у себя — то есть проверяли собственную копию вычислений,
    /// а не продукт. Здесь проверяется сам расчёт: координаты сверены с
    /// геометрией шаблона, а не с другой реализацией.
    /// </summary>
    [TestClass]
    public class DrillPatternTests
    {
        private static DrillPointsOperation Operation(DrillMode mode)
            => new DrillPointsOperation { DrillMode = mode, TotalDepth = 2, StepDepth = 1 };

        private static void AssertHoleAt(DrillHole hole, double x, double y, string message)
        {
            Assert.AreEqual(x, hole.X, 1e-9, $"{message}: X");
            Assert.AreEqual(y, hole.Y, 1e-9, $"{message}: Y");
        }

        /// <summary>
        /// Режим отдельных точек шаблона не имеет: отверстия задаёт
        /// пользователь, и построитель возвращает их без изменений.
        /// </summary>
        [TestMethod]
        public void Points_ReturnsUserDefinedHoles()
        {
            var operation = Operation(DrillMode.Points);
            operation.Holes.Add(new DrillHole { X = 3, Y = 4, TotalDepth = 5, StepDepth = 1 });

            var holes = operation.HolesToDrill;

            Assert.AreEqual(1, holes.Count);
            AssertHoleAt(holes[0], 3, 4, "Отверстие пользователя");
            Assert.AreEqual(5.0, holes[0].TotalDepth, "Собственные параметры отверстия сохраняются");
        }

        [TestMethod]
        public void Line_PlacesHolesAlongDirection()
        {
            var operation = Operation(DrillMode.Line);
            operation.StartX = 10;
            operation.StartY = 5;
            operation.Distance = 4;
            operation.HoleCount = 3;
            operation.AngleDeg = 90;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(3, holes.Count);
            AssertHoleAt(holes[0], 10, 5, "Первое отверстие в начальной точке");
            AssertHoleAt(holes[1], 10, 9, "Шаг вдоль направления 90°");
            AssertHoleAt(holes[2], 10, 13, "Третье отверстие");
        }

        /// <summary>Общие параметры глубины и подач переносятся в каждое отверстие.</summary>
        [TestMethod]
        public void Pattern_CopiesCommonDepthAndFeeds()
        {
            var operation = Operation(DrillMode.Line);
            operation.HoleCount = 2;
            operation.Distance = 5;
            operation.TotalDepth = 3.5;
            operation.StepDepth = 0.5;
            operation.FeedZRapid = 700;
            operation.FeedZWork = 150;
            operation.RetractHeight = 0.8;

            foreach (var hole in operation.HolesToDrill)
            {
                Assert.AreEqual(3.5, hole.TotalDepth);
                Assert.AreEqual(0.5, hole.StepDepth);
                Assert.AreEqual(700.0, hole.FeedZRapid);
                Assert.AreEqual(150.0, hole.FeedZWork);
                Assert.AreEqual(0.8, hole.RetractHeight);
            }
        }

        [TestMethod]
        public void Array_FillsGridWithPerpendicularRows()
        {
            var operation = Operation(DrillMode.Array);
            operation.Distance = 10;
            operation.HoleCount = 3;
            operation.RowPitch = 5;
            operation.RowCount = 2;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(6, holes.Count, "Сетка 3×2");
            AssertHoleAt(holes[0], 0, 0, "Начало сетки");
            AssertHoleAt(holes[2], 20, 0, "Конец первого ряда");
            AssertHoleAt(holes[3], 0, 5, "Второй ряд смещён перпендикулярно");
        }

        /// <summary>
        /// Прямоугольник — только периметр сетки: внутренние узлы пропускаются,
        /// иначе шаблон совпал бы с массивом.
        /// </summary>
        [TestMethod]
        public void Rectangle_KeepsOnlyPerimeter()
        {
            var operation = Operation(DrillMode.Rect);
            operation.Distance = 10;
            operation.HoleCount = 3;
            operation.RowPitch = 10;
            operation.RowCount = 3;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(8, holes.Count, "Из девяти узлов сетки 3×3 остаются восемь");
            Assert.IsFalse(
                holes.Any(h => Math.Abs(h.X - 10) < 1e-9 && Math.Abs(h.Y - 10) < 1e-9),
                "Центральный узел не сверлится");
        }

        [TestMethod]
        public void Circle_DistributesHolesEvenly()
        {
            var operation = Operation(DrillMode.Circle);
            operation.CenterX = 0;
            operation.CenterY = 0;
            operation.Radius = 10;
            operation.HoleCount = 4;
            operation.StartAngleDeg = 0;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(4, holes.Count);
            AssertHoleAt(holes[0], 10, 0, "Начальный угол 0°");
            AssertHoleAt(holes[1], 0, 10, "Четверть окружности");
            AssertHoleAt(holes[2], -10, 0, "Половина окружности");
            foreach (var hole in holes)
                Assert.AreEqual(10.0, Math.Sqrt(hole.X * hole.X + hole.Y * hole.Y), 1e-9, "Радиус");
        }

        /// <summary>
        /// В дуге отверстия расставляются от начального угла до конечного
        /// включительно: крайние отверстия попадают точно на концы дуги.
        /// </summary>
        [TestMethod]
        public void Arc_IncludesBothEnds()
        {
            var operation = Operation(DrillMode.Arc);
            operation.Radius = 10;
            operation.HoleCount = 3;
            operation.StartAngleDeg = 0;
            operation.EndAngleDeg = 90;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(3, holes.Count);
            AssertHoleAt(holes[0], 10, 0, "Начало дуги");
            AssertHoleAt(holes[2], 0, 10, "Конец дуги");
        }

        /// <summary>
        /// Совпадение начального и конечного углов означает полную окружность,
        /// но не дополнительное отверстие в начальной точке.
        /// </summary>
        [TestMethod]
        public void Arc_FullCircle_DoesNotRepeatTheFirstHole()
        {
            var operation = Operation(DrillMode.Arc);
            operation.Radius = 10;
            operation.HoleCount = 8;
            operation.StartAngleDeg = 0;
            operation.EndAngleDeg = 360;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(8, holes.Count);
            AssertHoleAt(holes[0], 10, 0, "Начало");
            AssertHoleAt(holes[2], 0, 10, "Четверть окружности");
            Assert.AreEqual(8, holes
                .Select(hole => (Math.Round(hole.X, 9), Math.Round(hole.Y, 9)))
                .Distinct()
                .Count(), "Каждая координата полного круга должна сверлиться один раз");
        }

        /// <summary>
        /// Многоугольник: первое отверстие каждой стороны попадает в вершину,
        /// остальные распределяются по стороне; вершина следующей стороны
        /// не задваивается.
        /// </summary>
        [TestMethod]
        public void Polygon_PlacesHolesOnVerticesAndSides()
        {
            var operation = Operation(DrillMode.Polygon);
            operation.Radius = 10;
            operation.NumberOfSides = 4;
            operation.HolesPerSide = 2;
            operation.RotationAngle = 0;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(8, holes.Count, "Четыре стороны по два отверстия");
            AssertHoleAt(holes[0], 10, 0, "Первая вершина");
            AssertHoleAt(holes[1], 5, 5, "Середина первой стороны");
            AssertHoleAt(holes[2], 0, 10, "Вторая вершина");
        }

        [TestMethod]
        public void Ellipse_AppliesRadiiAndRotation()
        {
            var operation = Operation(DrillMode.Ellipse);
            operation.RadiusX = 20;
            operation.RadiusY = 10;
            operation.HoleCount = 4;
            operation.StartAngleDeg = 0;
            operation.RotationAngle = 90;

            var holes = operation.HolesToDrill;

            Assert.AreEqual(4, holes.Count);
            AssertHoleAt(holes[0], 0, 20, "Большая полуось повёрнута на 90°");
            AssertHoleAt(holes[1], -10, 0, "Малая полуось повёрнута на 90°");
        }

        /// <summary>
        /// Двухрядный корпус: выводы нумеруются по кругу, поэтому второй ряд
        /// идёт в обратном направлении — как на самой микросхеме.
        /// </summary>
        [TestMethod]
        public void Package_TwoRows_NumbersPinsAround()
        {
            var operation = Operation(DrillMode.Package);
            operation.PackageName = "DIP8";

            var holes = operation.HolesToDrill;

            Assert.AreEqual(8, holes.Count, "DIP8 — восемь выводов");
            AssertHoleAt(holes[0], -3.81, -3.81, "Первый вывод");
            AssertHoleAt(holes[3], -3.81, 3.81, "Четвёртый вывод — конец первого ряда");
            AssertHoleAt(holes[4], 3.81, 3.81, "Пятый вывод — напротив четвёртого");
            AssertHoleAt(holes[7], 3.81, -3.81, "Восьмой вывод — напротив первого");
        }

        [TestMethod]
        public void Package_SingleRow_PlacesPinsInLine()
        {
            var operation = Operation(DrillMode.Package);
            operation.PackageName = "TO-220";

            var holes = operation.HolesToDrill;

            Assert.AreEqual(3, holes.Count);
            AssertHoleAt(holes[0], 0, -2.54, "Первый вывод");
            AssertHoleAt(holes[1], 0, 0, "Средний вывод в центре");
            AssertHoleAt(holes[2], 0, 2.54, "Третий вывод");
        }

        /// <summary>
        /// Неизвестное имя корпуса даёт корпус по умолчанию: операция
        /// с ещё не выбранным корпусом должна показывать осмысленный шаблон.
        /// </summary>
        [TestMethod]
        public void Package_UnknownName_UsesDefault()
        {
            var operation = Operation(DrillMode.Package);
            operation.PackageName = "НесуществующийКорпус";

            var holes = operation.HolesToDrill;

            Assert.AreEqual(8, holes.Count, "Корпус по умолчанию — DIP8");
        }

        /// <summary>
        /// Вырожденные параметры не дают отверстий: нулевое расстояние или
        /// отсутствие рядов означают, что шаблон ещё не задан.
        /// </summary>
        [TestMethod]
        public void DegenerateParameters_ProduceNoHoles()
        {
            var line = Operation(DrillMode.Line);
            line.HoleCount = 0;
            Assert.AreEqual(0, line.HolesToDrill.Count, "Линия без отверстий");

            var circle = Operation(DrillMode.Circle);
            circle.Radius = 0;
            circle.HoleCount = 4;
            Assert.AreEqual(0, circle.HolesToDrill.Count, "Окружность нулевого радиуса");

            var polygon = Operation(DrillMode.Polygon);
            polygon.NumberOfSides = 2;
            polygon.Radius = 10;
            Assert.AreEqual(0, polygon.HolesToDrill.Count, "Многоугольник с двумя сторонами");
        }

        /// <summary>
        /// Расстановка шаблона не хранится в файле проекта: её выводят из
        /// параметров. Прежде рядом с параметрами лежали и вычисленные по ним
        /// координаты — до тысяч записей на операцию, — а два описания одного
        /// и того же могли разойтись.
        /// </summary>
        [TestMethod]
        public void PatternHoles_AreNotStoredInProjectFile()
        {
            var operation = Operation(DrillMode.Array);
            operation.HoleCount = 20;
            operation.RowCount = 20;

            Assert.AreEqual(400, operation.HolesToDrill.Count, "Расстановка вычисляется");

            var json = new Persistence.ProjectFileService().Serialize(
                new List<OperationBase> { operation }, null);

            StringAssert.Contains(json, "\"HoleCount\":20", "Параметры шаблона сохраняются");
            StringAssert.Contains(json, "\"Holes\":[]", "Список отверстий остаётся пустым");
        }

        /// <summary>
        /// Файл, сохранённый прежней сборкой, содержит и параметры, и старые
        /// отверстия: сверлится расстановка по параметрам, а не то, что
        /// записано в файле.
        /// </summary>
        [TestMethod]
        public void PatternOperation_IgnoresStoredHolesFromOlderFiles()
        {
            var json = "{\"version\":4,\"operations\":[{\"type\":\"DrillPoints\",\"data\":{"
                + "\"DrillMode\":1,\"StartX\":0,\"StartY\":0,\"Distance\":10,\"HoleCount\":3,"
                + "\"TotalDepth\":2,\"StepDepth\":1,"
                + "\"Holes\":[{\"X\":999,\"Y\":999,\"TotalDepth\":2,\"StepDepth\":1}]}}]}";

            var loaded = (DrillPointsOperation)new Persistence.ProjectFileService()
                .Deserialize(json).Operations[0];

            Assert.AreEqual(3, loaded.HolesToDrill.Count, "Сверлится расстановка по параметрам");
            Assert.AreEqual(0.0, loaded.HolesToDrill[0].X, 1e-9);
            Assert.AreEqual(20.0, loaded.HolesToDrill[2].X, 1e-9);
        }

        /// <summary>
        /// Шаблон без операции — ошибка вызывающего, а не пустой список:
        /// иначе она проявилась бы операцией, которая ничего не сверлит.
        /// </summary>
        [TestMethod]
        public void Pattern_WithoutOperation_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DrillPatterns.For(DrillMode.Line).Holes(null));
        }

        /// <summary>
        /// У каждого способа расстановки есть свой шаблон: способ, добавленный
        /// в перечисление и в окно, но забытый в реестре, дал бы операцию
        /// без отверстий.
        /// </summary>
        [TestMethod]
        public void EveryDrillMode_HasItsOwnPattern()
        {
            var byPattern = new Dictionary<DrillPattern, DrillMode>();

            foreach (DrillMode mode in Enum.GetValues(typeof(DrillMode)))
            {
                var pattern = DrillPatterns.For(mode);

                Assert.AreEqual(mode, pattern.Mode, "Шаблон должен объявлять свой способ расстановки");
                Assert.IsFalse(byPattern.ContainsKey(pattern), $"{mode}: шаблон уже занят другим способом");
                byPattern[pattern] = mode;
            }

            Assert.AreEqual(Enum.GetValues(typeof(DrillMode)).Length, DrillPatterns.All.Count);
        }

        /// <summary>
        /// Незнакомый способ расстановки — отказ с указанием значения:
        /// файл проекта с неизвестным режимом не должен молча сверлиться
        /// каким-то другим.
        /// </summary>
        [TestMethod]
        public void UnknownDrillMode_IsRefusedWithItsValue()
        {
            var unknown = (DrillMode)Enum.GetValues(typeof(DrillMode)).Cast<int>().Max() + 1;

            var failure = Assert.Throws<NotSupportedException>(() => DrillPatterns.For(unknown));

            StringAssert.Contains(failure.Message, ((int)unknown).ToString());
        }

        /// <summary>
        /// Пороги каждого шаблона согласованы с его формулой: операция
        /// с параметрами по умолчанию проходит проверку шаблона и даёт
        /// непустую расстановку. Расхождение порога и формулы означало бы
        /// режим, который принимает параметры и молча ничего не сверлит, —
        /// ровно то, из-за чего пороги переехали из switch операции
        /// в сами шаблоны.
        /// </summary>
        [TestMethod]
        public void EveryPattern_DefaultsPassIssuesAndProduceHoles()
        {
            foreach (var entry in DrillPatterns.All)
            {
                var operation = DrillPointsOperation.CreateNew(entry.Key);
                if (entry.Key == DrillMode.Points)
                    operation.Holes.Add(new DrillHole { X = 0, Y = 0, TotalDepth = 2, StepDepth = 1 });

                var issues = new List<ValidationIssue>();
                entry.Value.AddIssues(issues, operation);

                Assert.AreEqual(0, issues.Count,
                    $"{entry.Key}: {string.Join("; ", issues)}");
                Assert.IsTrue(operation.HolesToDrill.Count > 0,
                    $"{entry.Key}: расстановка по умолчанию пуста");
            }
        }
    }
}
