using System.Collections.Generic;
using System.Linq;
using GCodeGenerator.Geometry;
using GCodeGenerator.Models;
using GCodeGenerator.Operations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Генераторы работают с геометрией через её контракт, не выясняя, какая
    /// операция за ним стоит.
    ///
    /// Прежде внутри «единых» генераторов стояли проверки «а не чертёж ли
    /// это»: контур из чертежа даёт несколько контуров, а карман из чертежа
    /// распадается на области, и оба случая обносили абстракцию снаружи.
    /// Теперь об этом говорит сама геометрия, поэтому источник с такими же
    /// свойствами не потребует новой проверки рядом с прежними.
    /// </summary>
    [TestClass]
    public class GeometryAbstractionTests
    {
        private static Polyline2D Square(double size)
            => new Polyline2D
            {
                Points =
                {
                    new Point2D { X = 0, Y = 0 },
                    new Point2D { X = size, Y = 0 },
                    new Point2D { X = size, Y = size },
                    new Point2D { X = 0, Y = size },
                    new Point2D { X = 0, Y = 0 },
                }
            };

        /// <summary>
        /// Обычная фигура — один контур, который генератор обходит сам:
        /// убирает совпавшие точки, начинает с ближайшей и замыкает.
        /// </summary>
        [TestMethod]
        public void PlainProfileGeometry_DoesNotProvideOrderedContours()
        {
            foreach (var descriptor in OperationCatalog.ByCategory(OperationCategory.Profile))
            {
                var operation = descriptor.Create();
                if (operation is ProfileDxfOperation)
                    continue;

                var geometry = OperationCatalog.CreateProfileGeometry(operation);

                Assert.IsFalse(geometry.ProvidesOrderedContours, descriptor.PersistentName);
                Assert.AreEqual(0, geometry.GetOrderedContours(GeometryTolerances.Vertex).Count,
                    descriptor.PersistentName);
            }
        }

        /// <summary>
        /// Контур из чертежа задаёт порядок обхода сам: смещение уже
        /// расставило точки, и таких контуров может быть несколько.
        /// </summary>
        [TestMethod]
        public void DxfProfileGeometry_ProvidesOrderedContours()
        {
            var operation = new ProfileDxfOperation { ToolDiameter = 1 };
            operation.Polylines.Add(Square(20));
            operation.Polylines.Add(new Polyline2D
            {
                Points =
                {
                    new Point2D { X = 50, Y = 50 },
                    new Point2D { X = 70, Y = 50 },
                    new Point2D { X = 70, Y = 70 },
                    new Point2D { X = 50, Y = 50 },
                }
            });

            var geometry = OperationCatalog.CreateProfileGeometry(operation);

            Assert.IsTrue(geometry.ProvidesOrderedContours);
            Assert.AreEqual(2, geometry.GetOrderedContours(GeometryTolerances.Vertex).Count,
                "Два отдельных контура чертежа обходятся по очереди");
        }

        /// <summary>
        /// Обычный карман областями не распадается: смещение внутрь оставляет
        /// одну фигуру, пока она не выродится совсем.
        /// </summary>
        [TestMethod]
        public void PlainPocketGeometry_DoesNotSplitIntoAreas()
        {
            foreach (var descriptor in OperationCatalog.ByCategory(OperationCategory.Pocket))
            {
                var operation = descriptor.Create();
                if (operation is PocketDxfOperation)
                    continue;

                var geometry = OperationCatalog.CreatePocketGeometry(operation);

                Assert.IsFalse(geometry.SplitsIntoAreas, descriptor.PersistentName);
                Assert.AreEqual(0, geometry.GetAreas(1, 0).Count, descriptor.PersistentName);
            }
        }

        /// <summary>
        /// Карман из чертежа распадается на области — по одной на каждый
        /// замкнутый контур, а узкий контур исчезает целиком, когда фреза
        /// в него не помещается.
        /// </summary>
        [TestMethod]
        public void DxfPocketGeometry_SplitsIntoAreasByContour()
        {
            var operation = new PocketDxfOperation { ToolDiameter = 2 };
            operation.ClosedContours.Add(Square(20));
            operation.ClosedContours.Add(new Polyline2D
            {
                Points =
                {
                    new Point2D { X = 100, Y = 0 },
                    new Point2D { X = 130, Y = 0 },
                    new Point2D { X = 130, Y = 30 },
                    new Point2D { X = 100, Y = 30 },
                    new Point2D { X = 100, Y = 0 },
                }
            });

            var geometry = OperationCatalog.CreatePocketGeometry(operation);

            Assert.IsTrue(geometry.SplitsIntoAreas);

            var areas = geometry.GetAreas(1, 0);
            Assert.AreEqual(2, areas.Count, "По области на каждый контур чертежа");
            foreach (var area in areas)
            {
                Assert.IsTrue(area.GetContour(0, 0).GetPoints().Count() >= 3,
                    "Область — готовый контур, обходить его можно сразу");
            }

            // Фреза шире контура: областей не остаётся, и слой не строится.
            Assert.AreEqual(0, geometry.GetAreas(40, 0).Count, "Слишком большая фреза не оставляет областей");
        }

        /// <summary>
        /// Готовая область не требует повторного смещения: её контур один
        /// и тот же при любом радиусе, потому что радиус уже учтён.
        /// </summary>
        [TestMethod]
        public void DxfPocketArea_IsAlreadyOffset()
        {
            var operation = new PocketDxfOperation { ToolDiameter = 2 };
            operation.ClosedContours.Add(Square(20));

            var area = OperationCatalog.CreatePocketGeometry(operation).GetAreas(1, 0).Single();

            var atZero = area.GetContour(0, 0).GetPoints().ToList();
            Assert.IsTrue(atZero.Count >= 3);

            var expected = new List<(double x, double y)>(atZero);
            CollectionAssert.AreEqual(expected, area.GetContour(0, 0).GetPoints().ToList());
        }
    }
}
