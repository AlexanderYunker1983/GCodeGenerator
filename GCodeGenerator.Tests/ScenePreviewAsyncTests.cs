using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Toolpath;
using GCodeGenerator.Trajectory;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Построение трёхмерной сцены не занимает поток интерфейса.
    ///
    /// Прежде сцена собиралась прямо в присваивании траектории: окно уже
    /// открыто, а обход перемещений и разбиение дуг идут на том же потоке,
    /// который рисует окно, — оно замирает. Генерация в фон вынесена давно,
    /// построение предпросмотра для той же программы оставалось синхронным.
    /// </summary>
    [TestClass]
    public class ScenePreviewAsyncTests
    {
        private static ToolPath PathWithMoves(int moveCount, double startX = 0)
        {
            var operation = new ToolPathOperation("Операция", "описание", 3);
            var builder = new ToolPathBuilder(operation);
            for (var i = 0; i < moveCount; i++)
                builder.LinearTo(x: startX + i, y: i, z: -1, feed: 100);

            var path = new ToolPath();
            path.AddOperation(operation);
            return path;
        }

        /// <summary>
        /// Сцена появляется не мгновенно, но появляется: присваивание
        /// траектории только запускает работу.
        /// </summary>
        [TestMethod]
        public async Task SettingToolPath_BuildsSceneInBackground()
        {
            var viewModel = new PreviewViewModel(null);

            viewModel.ToolPath = PathWithMoves(50);
            await WaitUntilBuilt(viewModel);

            Assert.IsTrue(viewModel.Scene.Segments.Count > 0, "Сцена построена");
            Assert.IsFalse(viewModel.IsBuilding, "Признак работы снят");
        }

        /// <summary>
        /// На время работы окно знает, что сцена строится: иначе оно выглядит
        /// пустым и неотзывчивым.
        /// </summary>
        [TestMethod]
        public async Task WhileBuilding_ViewModelReportsWork()
        {
            var viewModel = new PreviewViewModel(null);
            var reported = false;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PreviewViewModel.IsBuilding) && viewModel.IsBuilding)
                    reported = true;
            };

            viewModel.ToolPath = PathWithMoves(200);
            await WaitUntilBuilt(viewModel);

            Assert.IsTrue(reported, "О начале работы должно быть сообщено");
        }

        /// <summary>
        /// Пока строилась одна сцена, могли показать другую траекторию:
        /// поздний результат не должен затирать то, что показывают сейчас.
        /// </summary>
        [TestMethod]
        public async Task LateResult_DoesNotOverwriteNewerToolPath()
        {
            var viewModel = new PreviewViewModel(null);
            var first = PathWithMoves(400);
            var second = PathWithMoves(3, startX: 1000);

            viewModel.ToolPath = first;
            viewModel.ToolPath = second;
            await WaitUntilBuilt(viewModel);

            // У второй траектории перемещения начинаются за тысячей милли-
            // метров: по ним видно, чья сцена показана.
            Assert.IsTrue(viewModel.Scene.Segments.Count > 0);
            Assert.IsTrue(viewModel.Scene.Bounds.Value.Max.X >= 1000, "Показана последняя заданная траектория");
        }

        [TestMethod]
        public async Task ClearingToolPath_EmptiesScene()
        {
            var viewModel = new PreviewViewModel(null);
            viewModel.ToolPath = PathWithMoves(10);
            await WaitUntilBuilt(viewModel);

            viewModel.ToolPath = null;

            Assert.AreEqual(0, viewModel.Scene.Segments.Count, "Пустая траектория — пустая сцена");
        }

        // ------------------------------------------------------------------
        // Сбой построения
        // ------------------------------------------------------------------

        /// <summary>Журнал, запоминающий записи.</summary>
        private sealed class RecordingLogger : IAppLogger
        {
            public List<(LogLevel Level, string Message, Exception Exception)> Records { get; }
                = new List<(LogLevel, string, Exception)>();

            public void Log(LogLevel level, string message, Exception exception = null)
                => Records.Add((level, message, exception));
        }

        /// <summary>Окно, у которого построение сцены отказывает.</summary>
        private sealed class FailingPreviewViewModel : PreviewViewModel
        {
            public FailingPreviewViewModel(IAppLogger logger)
                : base(null, logger)
            {
            }

            protected override TrajectoryScene BuildScene(
                ToolPath toolPath,
                CancellationToken cancellationToken)
                => throw new InvalidOperationException("scene failure");
        }

        private sealed class BlockingPreviewViewModel : PreviewViewModel
        {
            private int _calls;

            public BlockingPreviewViewModel()
                : base(null)
            {
            }

            public ManualResetEventSlim FirstBuildStarted { get; } = new ManualResetEventSlim();

            public ManualResetEventSlim FirstBuildCancelled { get; } = new ManualResetEventSlim();

            protected override TrajectoryScene BuildScene(
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
        public async Task NewToolPath_CancelsTheOldSceneComputation()
        {
            var viewModel = new BlockingPreviewViewModel();
            viewModel.ToolPath = PathWithMoves(100);
            Assert.IsTrue(viewModel.FirstBuildStarted.Wait(TimeSpan.FromSeconds(2)));

            viewModel.ToolPath = PathWithMoves(3, startX: 1000);
            Assert.IsTrue(viewModel.FirstBuildCancelled.Wait(TimeSpan.FromSeconds(2)),
                "Старая 3D-сцена получила отмену");
            await WaitUntilBuilt(viewModel);

            Assert.IsTrue(viewModel.Scene.Bounds.Value.Max.X >= 1000,
                "После отмены показана новая траектория");
        }

        /// <summary>
        /// Сбой построения не теряется. Прежде задача запускалась без
        /// наблюдения за результатом: исключение уходило в
        /// UnobservedTaskException, журнал молчал, а пользователь видел
        /// пустое окно без объяснения.
        /// </summary>
        [TestMethod]
        public async Task SceneBuildFailure_IsLoggedAndShown()
        {
            var logger = new RecordingLogger();
            var viewModel = new FailingPreviewViewModel(logger);

            viewModel.ToolPath = PathWithMoves(10);
            await WaitUntilIdle(viewModel);

            Assert.IsTrue(viewModel.HasSceneError, "Окно знает о сбое");
            StringAssert.Contains(viewModel.SceneError, "scene failure", "Причина названа");
            Assert.IsFalse(viewModel.IsBuilding, "Признак работы снят");
            Assert.AreEqual(0, viewModel.Scene.Segments.Count, "Показывать нечего");

            var record = logger.Records.Single(r => r.Level == LogLevel.Error);
            Assert.IsNotNull(record.Exception, "Исключение ушло в журнал целиком");
        }

        /// <summary>
        /// Следующая траектория снимает сообщение о сбое: иначе оно осталось
        /// бы висеть поверх успешно построенной сцены.
        /// </summary>
        [TestMethod]
        public async Task NewToolPath_ClearsThePreviousFailure()
        {
            var viewModel = new FailingPreviewViewModel(new RecordingLogger());
            viewModel.ToolPath = PathWithMoves(10);
            await WaitUntilIdle(viewModel);
            Assert.IsTrue(viewModel.HasSceneError);

            viewModel.ToolPath = null;

            Assert.IsFalse(viewModel.HasSceneError, "Сообщение снято");
            Assert.AreEqual(string.Empty, viewModel.SceneError);
        }

        /// <summary>Ждёт окончания построения; тест не должен зависнуть насовсем.</summary>
        private static async Task WaitUntilBuilt(PreviewViewModel viewModel)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (!viewModel.IsBuilding && viewModel.Scene != TrajectoryScene.Empty)
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Сцена так и не построена");
        }

        /// <summary>
        /// Ждёт, пока построение закончится любым исходом: у отказавшего
        /// построения сцена так и остаётся пустой, и ждать её бессмысленно.
        /// </summary>
        private static async Task WaitUntilIdle(PreviewViewModel viewModel)
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                if (!viewModel.IsBuilding)
                    return;
                await Task.Delay(10);
            }

            Assert.Fail("Построение так и не закончилось");
        }
    }
}
