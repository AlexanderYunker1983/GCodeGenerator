using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.GCodeGenerators.Geometry;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Геометрия DXF-профиля: единственный расчёт смещения на радиус
    /// инструмента и сборка полилиний в контуры. Раньше тот же расчёт был
    /// продублирован в генераторе, и проверить его можно было только через
    /// готовый G-code.
    /// </summary>
    [TestClass]
    public class DxfProfileGeometryTests
    {
        private static DxfPolyline Poly(params (double X, double Y)[] points)
        {
            var polyline = new DxfPolyline();
            foreach (var (x, y) in points)
                polyline.Points.Add(new DxfPoint { X = x, Y = y });
            return polyline;
        }

        private static double PolygonArea(IReadOnlyList<(double x, double y)> points)
        {
            var asPoints = points.Select(p => new DxfPoint { X = p.x, Y = p.y }).ToList();
            return Geometry2D.Area(asPoints);
        }

        /// <summary>
        /// Замкнутый квадрат 10×10, фреза 4, обработка снаружи: траектория
        /// центра фрезы — квадрат 14×14. Смещение по усреднённой нормали
        /// отодвигало угол лишь на 2 мм по биссектрисе вместо 2·√2, из-за чего
        /// площадь получалась примерно на четверть меньше.
        /// </summary>
        [TestMethod]
        public void ClosedSquare_Outside_KeepsSquareCorners()
        {
            var op = new ProfileDxfOperation { ToolDiameter = 4, ToolPathMode = ToolPathMode.Outside };
            op.Polylines.Add(Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(14.0 * 14.0, PolygonArea(contours[0]), 1e-6);
        }

        [TestMethod]
        public void ClosedSquare_Inside_ShrinksByToolRadius()
        {
            var op = new ProfileDxfOperation { ToolDiameter = 4, ToolPathMode = ToolPathMode.Inside };
            op.Polylines.Add(Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(6.0 * 6.0, PolygonArea(contours[0]), 1e-6);
        }

        /// <summary>
        /// Режим «по линии» не смещает траекторию: контур совпадает с чертежом.
        /// </summary>
        [TestMethod]
        public void OnLine_KeepsSourceGeometry()
        {
            var op = new ProfileDxfOperation { ToolDiameter = 4, ToolPathMode = ToolPathMode.OnLine };
            op.Polylines.Add(Poly((0, 0), (10, 0), (10, 10), (0, 10), (0, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(10.0 * 10.0, PolygonArea(contours[0]), 1e-6);
            Assert.AreEqual(4, contours[0].Count, "Замыкающая точка не дублируется");
        }

        /// <summary>
        /// Отдельные отрезки чертежа, состыкованные концами, образуют один
        /// контур: инструмент проходит его без отрыва.
        /// </summary>
        [TestMethod]
        public void ConnectedPolylines_FormSingleContour()
        {
            var op = new ProfileDxfOperation { ToolPathMode = ToolPathMode.OnLine };
            op.Polylines.Add(Poly((0, 0), (10, 0)));
            op.Polylines.Add(Poly((10, 0), (10, 10)));
            op.Polylines.Add(Poly((10, 10), (0, 10)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            CollectionAssert.AreEqual(
                new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0) },
                contours[0].ToArray());
        }

        /// <summary>
        /// Стыковка учитывает направление: полилиния, записанная в обратном
        /// порядке, разворачивается при присоединении к цепочке.
        /// </summary>
        [TestMethod]
        public void ReversedPolyline_IsFlippedWhenJoined()
        {
            var op = new ProfileDxfOperation { ToolPathMode = ToolPathMode.OnLine };
            op.Polylines.Add(Poly((0, 0), (10, 0)));
            op.Polylines.Add(Poly((10, 10), (10, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            CollectionAssert.AreEqual(
                new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0) },
                contours[0].ToArray());
        }

        /// <summary>
        /// Несвязанные полилинии остаются отдельными контурами: между ними
        /// генератор поднимает инструмент на безопасную высоту.
        /// </summary>
        [TestMethod]
        public void DisconnectedPolylines_StaySeparateContours()
        {
            var op = new ProfileDxfOperation { ToolPathMode = ToolPathMode.OnLine };
            op.Polylines.Add(Poly((0, 0), (10, 0)));
            op.Polylines.Add(Poly((50, 0), (60, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(2, contours.Count);
        }

        /// <summary>
        /// Незамкнутая полилиния смещается сдвигом вершин по нормали: области
        /// у линии нет, поэтому смещение остаётся односторонним и число точек
        /// сохраняется.
        /// </summary>
        [TestMethod]
        public void OpenPolyline_Outside_ShiftsVerticesAlongNormal()
        {
            var op = new ProfileDxfOperation { ToolDiameter = 4, ToolPathMode = ToolPathMode.Outside };
            op.Polylines.Add(Poly((0, 0), (10, 0), (20, 0)));

            var contours = new DxfProfileGeometry(op).GetOffsetContours(GeometryTolerances.Vertex);

            Assert.AreEqual(1, contours.Count);
            Assert.AreEqual(3, contours[0].Count);
            foreach (var point in contours[0])
                Assert.AreEqual(2.0, point.y, 1e-9, "Линия вдоль X смещается на радиус фрезы по Y");
        }
    }
}
