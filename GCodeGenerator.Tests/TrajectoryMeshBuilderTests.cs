using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using GCodeGenerator.Trajectory;
using GCodeGenerator.Views.Scene;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Построение трёхмерной сцены: геометрия траектории и палитра.
    ///
    /// Раньше и то и другое считалось внутри одного метода рендера вместе с
    /// созданием объектов WPF, поэтому проверить сцену можно было только
    /// глазами на запущенном приложении.
    /// </summary>
    [TestClass]
    public class TrajectoryMeshBuilderTests
    {
        /// <summary>Вершин на один отрезок: коробка прямоугольного сечения.</summary>
        private const int VerticesPerSegment = 8;

        [TestMethod]
        public void EmptyScene_HasNoGeometryButKeepsAxes()
        {
            var meshes = TrajectoryMeshBuilder.Build(TrajectoryScene.Empty);

            Assert.AreEqual(0, meshes.Linear.Positions.Count);
            Assert.AreEqual(0, meshes.Rapid.Positions.Count);
            Assert.AreEqual(0, meshes.Markers.Count);
            Assert.AreEqual(10.0, meshes.AxisLength, "оси показываются и для пустой программы");
        }

        [TestMethod]
        public void LinearMove_GoesToLinearMeshOnly()
        {
            var meshes = TrajectoryMeshBuilder.Build(Scene(Segment(MoveType.Linear, 0, 0, 0, 10, 0, 0)));

            Assert.AreEqual(VerticesPerSegment, meshes.Linear.Positions.Count);
            Assert.AreEqual(0, meshes.Rapid.Positions.Count);
            Assert.AreEqual(0, meshes.ArcCW.Positions.Count);
            Assert.AreEqual(0, meshes.ArcCCW.Positions.Count);
        }

        [TestMethod]
        public void RapidMove_IsBrokenIntoDashes()
        {
            // 100 мм при штрихе 3 мм и промежутке 2 мм — двадцать штрихов.
            var meshes = TrajectoryMeshBuilder.Build(Scene(Segment(MoveType.Rapid, 0, 0, 0, 100, 0, 0)));

            var dashes = meshes.Rapid.Positions.Count / VerticesPerSegment;
            Assert.AreEqual(20, dashes);
            Assert.AreEqual(0, meshes.Linear.Positions.Count);
        }

        [TestMethod]
        public void Arc_IsDrawnByItsInterpolatedPoints()
        {
            var arc = Segment(MoveType.ArcCW, 0, 0, 0, 10, 0, 0);
            arc.InterpolatedPoints = new List<Vec3>
            {
                new Vec3(0, 0, 0), new Vec3(5, 5, 0), new Vec3(10, 0, 0)
            };

            var meshes = TrajectoryMeshBuilder.Build(Scene(arc));

            Assert.AreEqual(2 * VerticesPerSegment, meshes.ArcCW.Positions.Count, "две хорды по трём точкам");
            Assert.AreEqual(0, meshes.ArcCCW.Positions.Count);
        }

        [TestMethod]
        public void Sizes_FollowProgramExtent()
        {
            var small = TrajectoryMeshBuilder.Build(Scene(Segment(MoveType.Linear, 0, 0, 0, 10, 0, 0)));
            var large = TrajectoryMeshBuilder.Build(Scene(Segment(MoveType.Linear, 0, 0, 0, 1000, 0, 0)));

            Assert.IsTrue(large.LineThickness > small.LineThickness, "толстая линия для крупной программы");
            Assert.IsTrue(large.AxisLength > small.AxisLength, "длинные оси для крупной программы");
            Assert.AreEqual(0.05, small.LineThickness, 1e-9, "мелкая программа держит наименьшую толщину");
        }

        [TestMethod]
        public void Markers_MarkStartTransitionAndEnd()
        {
            var meshes = TrajectoryMeshBuilder.Build(Scene(
                Segment(MoveType.Rapid, 0, 0, 5, 10, 0, 5),
                Segment(MoveType.Linear, 10, 0, 5, 10, 0, -1),
                Segment(MoveType.Linear, 10, 0, -1, 20, 0, -1)));

            var roles = meshes.Markers.Select(m => m.Role).ToArray();
            CollectionAssert.AreEqual(
                new[] { MarkerRole.Start, MarkerRole.Transition, MarkerRole.End },
                roles);

            Assert.AreEqual(0.0, meshes.Markers[0].Position.X, "первая точка программы");
            Assert.AreEqual(20.0, meshes.Markers[2].Position.X, "последняя точка программы");
            Assert.IsTrue(meshes.Markers[0].Radius > meshes.Markers[1].Radius, "начало заметнее перехода");
        }

        [TestMethod]
        public void DarkTheme_LightensAxesAndOrigin()
        {
            var light = SceneMaterials.ForBackground(Colors.White);
            var dark = SceneMaterials.ForBackground(Color.FromRgb(37, 37, 37));

            Assert.IsFalse(light.IsDarkBackground);
            Assert.IsTrue(dark.IsDarkBackground);
            Assert.AreEqual(Colors.White, ColorOf(dark.Origin), "на тёмном фоне ноль детали светлый");
            Assert.AreEqual(Color.FromRgb(40, 40, 40), ColorOf(light.Origin), "на светлом — тёмный");
            Assert.IsTrue(ColorOf(dark.ZAxis).B > ColorOf(light.ZAxis).B, "синяя ось на тёмном фоне светлее");
        }

        [TestMethod]
        public void MoveColors_DoNotDependOnTheme()
        {
            var light = SceneMaterials.ForBackground(Colors.White);
            var dark = SceneMaterials.ForBackground(Colors.Black);

            // По цвету читают тип перемещения, поэтому он один и тот же в любой теме.
            Assert.AreEqual(ColorOf(light.Rapid), ColorOf(dark.Rapid));
        }

        [TestMethod]
        public void CompletedSceneResources_AreFrozen()
        {
            var scene = Scene(
                Segment(MoveType.Rapid, 0, 0, 5, 10, 0, 5),
                Segment(MoveType.Linear, 10, 0, 5, 10, 0, -1));
            var meshes = TrajectoryMeshBuilder.Build(scene);
            var materials = SceneMaterials.ForBackground(Colors.White);
            var model = GCodeGenerator.Views.SceneRenderer.Render(scene, materials);

            Assert.IsTrue(meshes.Rapid.IsFrozen);
            Assert.IsTrue(meshes.Linear.IsFrozen);
            Assert.IsTrue(materials.BackgroundBrush.IsFrozen);
            Assert.IsTrue(materials.Rapid.IsFrozen);
            Assert.IsTrue(materials.Linear.IsFrozen);
            Assert.IsTrue(model.IsFrozen,
                "Группа рекурсивно фиксирует оси, маркеры и их геометрию");
        }

        private static Color ColorOf(System.Windows.Media.Media3D.Material material)
        {
            var diffuse = material as System.Windows.Media.Media3D.DiffuseMaterial;
            Assert.IsNotNull(diffuse, "ожидался матовый материал");
            return ((SolidColorBrush)diffuse.Brush).Color;
        }

        private static TrajectoryScene Scene(params TrajectorySegment[] segments)
            => new TrajectoryScene(segments);

        private static TrajectorySegment Segment(MoveType moveType,
            double x1, double y1, double z1, double x2, double y2, double z2)
            => new TrajectorySegment
            {
                Start = new Vec3(x1, y1, z1),
                End = new Vec3(x2, y2, z2),
                MoveType = moveType
            };
    }
}
