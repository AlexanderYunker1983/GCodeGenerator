using System;
using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Численный инвариант эквидистанты профиля: каждая точка траектории
    /// отстоит от идеального контура фигуры не меньше чем на смещение —
    /// ближе значит зарез детали радиусом фрезы, — и хотя бы часть точек
    /// касается смещения, иначе траектория ушла от детали и размер не
    /// выдержан. Идеал — та же геометрия с нулевым смещением: режим
    /// «по линии» даёт саму фигуру.
    ///
    /// Инвариант existовал только неявно: эталоны профилей построены в
    /// режиме OnLine, где смещения нет, и эквидистанта многоугольника,
    /// считавшая смещение по радиусу вершины вместо расстояния между
    /// сторонами, вместе с «эквидистантой» эллипса, прибавлявшей смещение
    /// к полуосям, жили в невидимой для эталонов зоне.
    /// </summary>
    [TestClass]
    public class ProfileEquidistantTests
    {
        /// <summary>Радиус фрезы Default-фикстур: диаметр 3 мм.</summary>
        private const double Offset = 1.5;

        /// <summary>
        /// Допуск сравнения: хорды тесселяции идеала и траектории плюс
        /// квантование стыков Clipper. Дефекты, ради которых написан тест,
        /// на порядок больше: у шестиугольника недобор смещения составлял
        /// 13 % (0.2 мм при смещении 1.5), у эллипса 30×16 — до 0.3 мм.
        /// </summary>
        private const double Tolerance = 0.05;

        private static IEnumerable<(string Name, OperationBase Operation)> Shapes()
        {
            yield return ("Rectangle", OperationFixtures.ProfileRectangle());
            yield return ("RoundedRectangle", OperationFixtures.ProfileRoundedRectangle());
            yield return ("Circle", OperationFixtures.ProfileCircle());
            yield return ("Ellipse", OperationFixtures.ProfileEllipse());
            yield return ("Polygon", OperationFixtures.ProfilePolygon());
        }

        /// <summary>Наружная обработка: смещение наружу на радиус фрезы.</summary>
        [TestMethod]
        public void OutsideToolPath_KeepsToolRadiusFromContour()
            => AssertEquidistant(+Offset);

        /// <summary>Внутренняя обработка: смещение внутрь на радиус фрезы.</summary>
        [TestMethod]
        public void InsideToolPath_KeepsToolRadiusFromContour()
            => AssertEquidistant(-Offset);

        private static void AssertEquidistant(double toolOffset)
        {
            var failures = new List<string>();

            foreach (var (name, operation) in Shapes())
            {
                var descriptor = OperationCatalog.ForType(operation.GetType());
                var geometry = descriptor.CreateProfileGeometry(operation);

                var ideal = geometry.GetContourPoints(0.0, MillingDirection.Clockwise).ToList();
                var path = geometry.GetContourPoints(toolOffset, MillingDirection.Clockwise).ToList();
                if (ideal.Count < 2 || path.Count < 2)
                {
                    failures.Add($"{name}: контур пуст (идеал {ideal.Count}, траектория {path.Count})");
                    continue;
                }

                // Точки берутся вдоль сегментов траектории, а не только в её
                // вершинах: фреза режет всей стороной, а вершины смещённого
                // прямоугольника — углы-миттеры — честно дальше смещения.
                var closest = double.MaxValue;
                foreach (var point in SampleAlong(path, 0.5))
                {
                    var distance = DistanceToContour(point, ideal);
                    closest = Math.Min(closest, distance);
                }

                if (closest < Math.Abs(toolOffset) - Tolerance)
                {
                    failures.Add(FormattableString.Invariant(
                        $"{name}: траектория подходит к контуру на {closest:0.000} при смещении {Math.Abs(toolOffset):0.000} — зарез"));
                }

                if (closest > Math.Abs(toolOffset) + Tolerance)
                {
                    failures.Add(FormattableString.Invariant(
                        $"{name}: ближайшая точка траектории в {closest:0.000} от контура — траектория не касается смещения"));
                }
            }

            Assert.AreEqual(0, failures.Count, string.Join("; ", failures));
        }

        /// <summary>Точки вдоль ломаной с шагом не крупнее указанного.</summary>
        private static IEnumerable<(double x, double y)> SampleAlong(
            List<(double x, double y)> path, double maxStep)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                var length = Geometry2D.Distance(from.x, from.y, to.x, to.y);
                var steps = Math.Max(1, (int)Math.Ceiling(length / maxStep));
                for (int s = 0; s < steps; s++)
                {
                    var t = (double)s / steps;
                    yield return (from.x + t * (to.x - from.x), from.y + t * (to.y - from.y));
                }
            }

            yield return path[path.Count - 1];
        }

        /// <summary>Расстояние от точки до замкнутого идеального контура.</summary>
        private static double DistanceToContour((double x, double y) point, List<(double x, double y)> contour)
        {
            var distance = double.MaxValue;
            for (int i = 0; i < contour.Count - 1; i++)
            {
                distance = Math.Min(distance, Geometry2D.DistanceToSegment(
                    point.x, point.y,
                    contour[i].x, contour[i].y,
                    contour[i + 1].x, contour[i + 1].y,
                    GeometryTolerances.Degenerate));
            }

            return distance;
        }
    }
}
