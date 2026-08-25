using System;
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
    public class DrillPatternBuilderTests
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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            foreach (var hole in DrillPatternBuilder.Build(operation))
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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

            Assert.AreEqual(3, holes.Count);
            AssertHoleAt(holes[0], 10, 0, "Начало дуги");
            AssertHoleAt(holes[2], 0, 10, "Конец дуги");
        }

        /// <summary>Совпадение начального и конечного углов означает полную окружность.</summary>
        [TestMethod]
        public void Arc_ZeroSpan_BecomesFullCircle()
        {
            var operation = Operation(DrillMode.Arc);
            operation.Radius = 10;
            operation.HoleCount = 5;
            operation.StartAngleDeg = 0;
            operation.EndAngleDeg = 0;

            var holes = DrillPatternBuilder.Build(operation);

            Assert.AreEqual(5, holes.Count);
            AssertHoleAt(holes[0], 10, 0, "Начало");
            AssertHoleAt(holes[4], 10, 0, "Последнее отверстие возвращается к началу");
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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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

            var holes = DrillPatternBuilder.Build(operation);

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
            Assert.AreEqual(0, DrillPatternBuilder.Build(line).Count, "Линия без отверстий");

            var circle = Operation(DrillMode.Circle);
            circle.Radius = 0;
            circle.HoleCount = 4;
            Assert.AreEqual(0, DrillPatternBuilder.Build(circle).Count, "Окружность нулевого радиуса");

            var polygon = Operation(DrillMode.Polygon);
            polygon.NumberOfSides = 2;
            polygon.Radius = 10;
            Assert.AreEqual(0, DrillPatternBuilder.Build(polygon).Count, "Многоугольник с двумя сторонами");
        }

        [TestMethod]
        public void Build_Null_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => DrillPatternBuilder.Build(null));
        }
    }
}
