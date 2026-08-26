using System.Threading.Tasks;
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
    }
}
