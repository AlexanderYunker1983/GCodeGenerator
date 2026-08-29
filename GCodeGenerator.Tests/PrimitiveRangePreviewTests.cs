#nullable enable
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.Toolpath;
using GCodeGenerator.Trajectory;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Ограничение 3D-предпросмотра диапазоном прямых и дуг.</summary>
    [TestClass]
    [SupportedOSPlatform("windows")]
    public sealed class PrimitiveRangePreviewTests
    {
        [TestMethod]
        public async Task ViewModel_CountsElementaryPrimitivesAcrossOperationBoundaries()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = FourPrimitivePath();
            await WaitUntilBuilt(viewModel);

            Assert.AreEqual(4, viewModel.PrimitiveCount);
            Assert.AreEqual(1, viewModel.FirstPreviewPrimitive);
            Assert.AreEqual(4, viewModel.LastPreviewPrimitive);
            Assert.AreEqual("1. Rapid", viewModel.FirstPreviewPrimitiveText);
            Assert.AreEqual("4. ArcCCW", viewModel.LastPreviewPrimitiveText);
        }

        [TestMethod]
        public async Task ChangingBounds_SelectsSegmentsWithoutRebuildingToolPath()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = FourPrimitivePath();
            await WaitUntilBuilt(viewModel);

            viewModel.FirstPreviewPrimitive = 2;
            viewModel.LastPreviewPrimitive = 2;

            Assert.IsFalse(viewModel.IsBuilding, "Готовая сцена фильтруется без фоновой пересборки");
            Assert.AreEqual(1, viewModel.Scene!.Segments.Count);
            Assert.AreEqual(MoveType.Linear, viewModel.Scene.Segments[0].MoveType);
            Assert.AreEqual(10, viewModel.Scene.Segments[0].Start.X, 1e-9);
            Assert.AreEqual(20, viewModel.Scene.Segments[0].End.X, 1e-9);
            Assert.AreEqual("2. Linear", viewModel.FirstPreviewPrimitiveText);
            Assert.AreEqual(viewModel.FirstPreviewPrimitiveText, viewModel.LastPreviewPrimitiveText);
        }

        [TestMethod]
        public async Task ReplacingToolPath_ResetsRangeToAllNewPrimitives()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = FourPrimitivePath();
            await WaitUntilBuilt(viewModel);
            viewModel.FirstPreviewPrimitive = 2;
            viewModel.LastPreviewPrimitive = 2;

            viewModel.ToolPath = LinearPath(100, 200);
            await WaitUntilBuilt(viewModel);

            Assert.AreEqual(2, viewModel.PrimitiveCount);
            Assert.AreEqual(1, viewModel.FirstPreviewPrimitive);
            Assert.AreEqual(2, viewModel.LastPreviewPrimitive);
            Assert.AreEqual(2, viewModel.Scene!.Segments.Count);
        }

        [TestMethod]
        public async Task PreviewView_BindsMahAppsRangeSliderToPrimitiveBounds()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = FourPrimitivePath();
            await WaitUntilBuilt(viewModel);

            TestApplication.Run(() =>
            {
                var view = new PreviewView { DataContext = viewModel };
                try
                {
                    view.Show();
                    view.UpdateLayout();

                    Assert.AreEqual(1, view.PrimitiveRangeSlider.Minimum, 1e-9);
                    Assert.AreEqual(4, view.PrimitiveRangeSlider.Maximum, 1e-9);
                    Assert.AreEqual(1, view.PrimitiveRangeSlider.LowerValue, 1e-9);
                    Assert.AreEqual(4, view.PrimitiveRangeSlider.UpperValue, 1e-9);
                    Assert.IsTrue(view.PrimitiveRangeSlider.IsSnapToTickEnabled);
                }
                finally
                {
                    view.Close();
                }
            });
        }

        [TestMethod]
        public async Task ChangingPrimitiveRange_PreservesUserCameraProjection()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = FourPrimitivePath();
            await WaitUntilBuilt(viewModel);

            TestApplication.Run(() =>
            {
                var view = new PreviewView { DataContext = viewModel };
                try
                {
                    view.Show();
                    view.UpdateLayout();

                    var position = new Point3D(31, -47, 83);
                    var lookDirection = new Vector3D(-7, 11, -13);
                    var upDirection = new Vector3D(2, 3, 5);
                    view.Camera.Position = position;
                    view.Camera.LookDirection = lookDirection;
                    view.Camera.UpDirection = upDirection;

                    viewModel.FirstPreviewPrimitive = 2;
                    viewModel.LastPreviewPrimitive = 3;
                    view.UpdateLayout();

                    Assert.AreEqual(position, view.Camera.Position);
                    Assert.AreEqual(lookDirection, view.Camera.LookDirection);
                    Assert.AreEqual(upDirection, view.Camera.UpDirection);
                }
                finally
                {
                    view.Close();
                }
            });
        }

        /// <summary>
        /// Рабочий поток передаёт в окно Program, а не ToolPath. Пустая сцена
        /// уже содержит модели осей, но не должна считаться основанием для
        /// первого кадрирования: поздняя программа с большим офсетом обязана
        /// переставить камеру к своей фактической геометрии.
        /// </summary>
        [TestMethod]
        public async Task EmptyAxes_DoNotConsumeAutoFitBeforeProgramArrives()
        {
            PreviewView? view = null;
            var viewModel = new PreviewViewModel(null!);
            TestApplication.Run(() =>
            {
                view = new PreviewView { DataContext = viewModel };
                view.Show();
                view.UpdateLayout();
                viewModel.Program = new GCodeGenerators.GenericPostProcessor().Build(
                    OffsetPath(), new Models.GCodeSettings());
            });

            try
            {
                await WaitUntilBuilt(viewModel);
                TestApplication.Run(() =>
                {
                    view!.UpdateLayout();
                    var target = view.Camera.Position + view.Camera.LookDirection;
                    Assert.IsTrue(target.X > 400 && target.Y > 400,
                        $"камера осталась у служебных осей: target={target}");
                });
            }
            finally
            {
                TestApplication.Run(() => view?.Close());
            }
        }

        [TestMethod]
        public async Task ShowAllButton_RestoresCameraAfterManualNavigation()
        {
            var viewModel = new PreviewViewModel(null!);
            viewModel.ToolPath = OffsetPath();
            await WaitUntilBuilt(viewModel);

            TestApplication.Run(() =>
            {
                var view = new PreviewView { DataContext = viewModel };
                try
                {
                    view.Show();
                    view.UpdateLayout();
                    view.Camera.Position = new Point3D(-500, -500, 10);
                    view.Camera.LookDirection = new Vector3D(0, 0, -1);

                    view.FitCameraButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

                    var target = view.Camera.Position + view.Camera.LookDirection;
                    Assert.IsTrue(target.X > 400 && target.Y > 400,
                        "кнопка возвращает к bounds траектории");
                }
                finally
                {
                    view.Close();
                }
            });
        }

        [TestMethod]
        public void CameraDistance_UsesFieldOfViewAndViewportAspect()
        {
            var square = PreviewView.CameraDistance(100, 100, 0, 45, 1);
            var wideViewport = PreviewView.CameraDistance(100, 100, 0, 45, 2);
            var narrowFov = PreviewView.CameraDistance(100, 100, 0, 25, 1);

            Assert.IsTrue(wideViewport > square, "вертикальный размер труднее вписать в широкий viewport");
            Assert.IsTrue(narrowFov > square, "узкий угол обзора требует большего расстояния");
        }

        private static ToolPath FourPrimitivePath()
        {
            var firstOperation = new ToolPathOperation("Первая", string.Empty, 3);
            var firstBuilder = new ToolPathBuilder(firstOperation);
            firstBuilder.RapidTo(x: 10, y: 0, z: 0, feed: 500);
            firstBuilder.LinearTo(x: 20, y: 0, z: 0, feed: 100);

            var secondOperation = new ToolPathOperation("Вторая", string.Empty, 3);
            var secondBuilder = new ToolPathBuilder(secondOperation);
            secondBuilder.ArcCW(x: 20, y: 10, i: 0, j: 5, feed: 100);
            secondBuilder.ArcCCW(x: 20, y: 0, i: 0, j: -5, feed: 100);

            var path = new ToolPath();
            path.AddOperation(firstOperation);
            path.AddOperation(secondOperation);
            return path;
        }

        private static ToolPath LinearPath(params double[] endPoints)
        {
            var operation = new ToolPathOperation("Прямые", string.Empty, 3);
            var builder = new ToolPathBuilder(operation);
            foreach (var endPoint in endPoints)
                builder.LinearTo(x: endPoint, y: 0, z: 0, feed: 100);

            var path = new ToolPath();
            path.AddOperation(operation);
            return path;
        }

        private static ToolPath OffsetPath()
        {
            var operation = new ToolPathOperation("Со смещением", string.Empty, 3);
            var builder = new ToolPathBuilder(operation);
            builder.RapidTo(x: 1000, y: 1000, z: 0, feed: 500);
            builder.LinearTo(x: 1200, y: 1100, z: -10, feed: 100);

            var path = new ToolPath();
            path.AddOperation(operation);
            return path;
        }

        private static async Task WaitUntilBuilt(PreviewViewModel viewModel)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (!viewModel.IsBuilding && viewModel.PrimitiveCount > 0)
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Полная сцена так и не построена");
        }
    }
}
