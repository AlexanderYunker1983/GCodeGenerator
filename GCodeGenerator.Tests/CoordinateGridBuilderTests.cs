#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using GCodeGenerator.Trajectory;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views.Scene;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Адаптивные координатные сетки трёхмерного предпросмотра.</summary>
    [TestClass]
    public sealed class CoordinateGridBuilderTests
    {
        [TestMethod]
        public void EmptyScene_GetsUsefulDefaultScale()
        {
            var layout = CoordinateGridBuilder.CreateLayout(TrajectoryScene.Empty);

            Assert.AreEqual(2.0, layout.Step, 1e-9);
            Assert.IsTrue(layout.X.Minimum < 0 && layout.X.Maximum > 0);
            Assert.IsTrue(layout.Y.Minimum < 0 && layout.Y.Maximum > 0);
            Assert.IsTrue(layout.Z.Minimum < 0 && layout.Z.Maximum > 0);
            Assert.IsTrue(layout.X.Ticks.Contains(0.0));
        }

        [TestMethod]
        public void Layout_UsesOneNiceStepAndCoversTrajectoryAndOrigin()
        {
            var scene = Scene(Segment(-23, 4, -7, 87, 34, 13));

            var layout = CoordinateGridBuilder.CreateLayout(scene);

            Assert.AreEqual(20.0, layout.Step, 1e-9, "ряд шага 1–2–5");
            Assert.IsTrue(layout.X.Minimum <= -23 && layout.X.Maximum >= 87);
            Assert.IsTrue(layout.Y.Minimum <= 0 && layout.Y.Maximum >= 34);
            Assert.IsTrue(layout.Z.Minimum <= -7 && layout.Z.Maximum >= 13);
            Assert.AreEqual(layout.Step, layout.X.Ticks[1] - layout.X.Ticks[0], 1e-9);
            Assert.AreEqual(layout.Step, layout.Y.Ticks[1] - layout.Y.Ticks[0], 1e-9);
            Assert.AreEqual(layout.Step, layout.Z.Ticks[1] - layout.Z.Ticks[0], 1e-9);
        }

        [TestMethod]
        public void EveryPlane_HasGridLinesAndNumericLabelsInItsOwnPlane()
        {
            var scene = Scene(Segment(-10, -5, -3, 20, 15, 7));
            var grids = CoordinateGridBuilder.Build(
                scene,
                SceneMaterials.ForBackground(Colors.White));

            foreach (var plane in new[] { grids.Xy, grids.Xz, grids.Yz })
            {
                Assert.IsTrue(plane.Lines.Positions.Count > 0, "линии сетки");
                Assert.IsTrue(plane.Labels.Positions.Count > 0, "числовые отметки");
                Assert.AreEqual(2, plane.Model.Children.Count, "линии и отметки — отдельные меши");
                Assert.IsTrue(plane.Lines.IsFrozen);
                Assert.IsTrue(plane.Labels.IsFrozen);
                Assert.IsTrue(plane.Model.IsFrozen);
            }

            Assert.IsTrue(grids.Xy.Lines.Bounds.SizeZ < grids.Layout.LineThickness * 2,
                "XY лежит при Z = 0");
            Assert.IsTrue(grids.Xz.Lines.Bounds.SizeY < grids.Layout.LineThickness * 2,
                "XZ лежит при Y = 0");
            Assert.IsTrue(grids.Yz.Lines.Bounds.SizeX < grids.Layout.LineThickness * 2,
                "YZ лежит при X = 0");
        }

        [TestMethod]
        public void ViewModel_TogglesEveryPlaneIndependently()
        {
            var viewModel = new PreviewViewModel(null!);
            var changed = new List<string>();
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);

            viewModel.ShowXyGrid = true;
            viewModel.ShowXzGrid = true;

            Assert.IsTrue(viewModel.ShowXyGrid);
            Assert.IsTrue(viewModel.ShowXzGrid);
            Assert.IsFalse(viewModel.ShowYzGrid);
            CollectionAssert.Contains(changed, nameof(viewModel.ShowXyGrid));
            CollectionAssert.Contains(changed, nameof(viewModel.ShowXzGrid));

            viewModel.ShowXyGrid = false;
            viewModel.ShowYzGrid = true;

            Assert.IsFalse(viewModel.ShowXyGrid);
            Assert.IsTrue(viewModel.ShowXzGrid);
            Assert.IsTrue(viewModel.ShowYzGrid);
        }

        private static TrajectoryScene Scene(params TrajectorySegment[] segments)
            => new TrajectoryScene(segments);

        private static TrajectorySegment Segment(
            double x1,
            double y1,
            double z1,
            double x2,
            double y2,
            double z2)
            => new TrajectorySegment
            {
                Start = new Vec3(x1, y1, z1),
                End = new Vec3(x2, y2, z2),
                MoveType = MoveType.Linear,
            };
    }
}
