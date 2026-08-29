using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.Services;
using GCodeGenerator.Toolpath;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Большая двумерная траектория не должна занимать UI-поток.</summary>
    [TestClass]
    public class OperationsPreviewAsyncTests
    {
        private sealed class ThemeService : IThemeService
        {
#pragma warning disable CS0067
            public event EventHandler ThemeChanged;
#pragma warning restore CS0067
            public void ApplyTheme(bool useDarkTheme) { }
        }

        private sealed class BlockingPreview : OperationsPreviewViewModel
        {
            private int _calls;

            public BlockingPreview(ObservableCollection<OperationBase> operations)
                : base(operations, new ThemeService())
            {
            }

            public ManualResetEventSlim FirstBuildStarted { get; } = new ManualResetEventSlim();

            public ManualResetEventSlim FirstBuildCancelled { get; } = new ManualResetEventSlim();

            protected override OperationScene BuildScene(
                ToolPath toolPath,
                CancellationToken cancellationToken)
            {
                if (Interlocked.Increment(ref _calls) == 1)
                {
                    FirstBuildStarted.Set();
                    try
                    {
                        cancellationToken.WaitHandle.WaitOne();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException)
                    {
                        FirstBuildCancelled.Set();
                        throw;
                    }
                }

                return base.BuildScene(toolPath, cancellationToken);
            }
        }

        [TestMethod]
        public async Task SwitchingToToolPath_ReturnsImmediatelyAndBuildsInBackground()
        {
            var operation = new ProfileCircleOperation();
            var preview = new BlockingPreview(new ObservableCollection<OperationBase> { operation })
            {
                ToolPath = Path(operation, 10)
            };

            var stopwatch = Stopwatch.StartNew();
            preview.ShowToolPath = true;
            stopwatch.Stop();

            Assert.IsTrue(preview.FirstBuildStarted.Wait(TimeSpan.FromSeconds(2)), "Фоновая работа началась");
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500),
                $"Setter занял UI-поток на {stopwatch.ElapsedMilliseconds} мс");
            Assert.IsTrue(preview.IsBuilding);

            preview.ShowToolPath = false;
            Assert.IsTrue(preview.FirstBuildCancelled.Wait(TimeSpan.FromSeconds(2)), "Старая работа получила отмену");
            await WaitUntilIdle(preview);
            Assert.IsTrue(preview.Scene.Shapes.Count > 0, "После отмены показаны контуры");
        }

        [TestMethod]
        public async Task NewToolPath_CancelsOldProjectionAndOnlyNewResultWins()
        {
            var operation = new ProfileCircleOperation();
            var preview = new BlockingPreview(new ObservableCollection<OperationBase> { operation })
            {
                ToolPath = Path(operation, 100)
            };
            preview.ShowToolPath = true;
            Assert.IsTrue(preview.FirstBuildStarted.Wait(TimeSpan.FromSeconds(2)));

            preview.ToolPath = Path(operation, 3, startX: 1000);
            Assert.IsTrue(preview.FirstBuildCancelled.Wait(TimeSpan.FromSeconds(2)));
            await WaitUntilIdle(preview);

            Assert.IsTrue(preview.Scene.Shapes.Count > 0);
            Assert.IsTrue(preview.Scene.Bounds!.Value.MaxX >= 1000,
                "Поздний результат старой проекции не затёр новую сцену");
        }

        private static ToolPath Path(OperationBase source, int moves, double startX = 0)
        {
            var operation = new ToolPathOperation("path", "path", 3, source);
            var builder = new ToolPathBuilder(operation);
            for (var i = 0; i < moves; i++)
                builder.LinearTo(x: startX + i, y: i, feed: 100);
            var path = new ToolPath();
            path.AddOperation(operation);
            return path;
        }

        private static async Task WaitUntilIdle(OperationsPreviewViewModel preview)
        {
            for (var attempt = 0; attempt < 300; attempt++)
            {
                if (!preview.IsBuilding)
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Фоновая 2D-проекция не завершилась");
        }
    }
}
