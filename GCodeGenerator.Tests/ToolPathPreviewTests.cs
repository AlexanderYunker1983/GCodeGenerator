using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Двумерный предпросмотр умеет показывать саму траекторию.
    ///
    /// Прежний вид — контуры, построенные заново из моделей операций —
    /// показывает замысел: где лежит окружность, каких размеров карман.
    /// Он ничего не знает ни о компенсации радиуса фрезы, ни о выбранной
    /// стратегии выборки, ни о числе проходов, поэтому карман выглядел
    /// пустым овалом, а контур — линией по чертежу, а не по центру фрезы.
    /// </summary>
    [TestClass]
    public class ToolPathPreviewTests
    {
        private static Toolpath.ToolPath BuildPath(params OperationBase[] operations)
            => new SimpleGCodeGenerator().BuildToolPath(new List<OperationBase>(operations), new GCodeSettings());

        [TestMethod]
        public void Projection_SeparatesCuttingFromRapidMoves()
        {
            var scene = ToolPathSceneProjection.Build(BuildPath(OperationFixtures.PocketCircle()));

            Assert.IsTrue(scene.Shapes.Any(s => s.Kind == OperationShapeKind.CuttingMove),
                "Рабочие ходы показаны");
            Assert.IsTrue(scene.Shapes.Any(s => s.Kind == OperationShapeKind.RapidMove),
                "Холостые переходы показаны отдельно");
        }

        /// <summary>
        /// Ради этого вид и нужен: выборка кармана — это множество проходов,
        /// а не один контур, и на глаз это видно только по траектории.
        /// </summary>
        [TestMethod]
        public void PocketToolPath_ShowsMoreThanItsContour()
        {
            var pocket = OperationFixtures.PocketCircle();

            var contourScene = OperationSceneBuilder.Build(new[] { pocket });
            var toolPathScene = ToolPathSceneProjection.Build(BuildPath(pocket));

            var contourPoints = contourScene.Shapes.Sum(s => s.Points.Count);
            var toolPathPoints = toolPathScene.Shapes.Sum(s => s.Points.Count);

            Assert.IsTrue(toolPathPoints > contourPoints,
                $"Траектория выборки подробнее контура: {toolPathPoints} против {contourPoints}");
        }

        /// <summary>
        /// Каждая фигура помнит свою операцию: предпросмотр подсвечивает
        /// выбранную и открывает её по двойному щелчку.
        /// </summary>
        [TestMethod]
        public void EveryShape_KnowsItsOperation()
        {
            var drill = OperationFixtures.DrillPoints();
            var profile = OperationFixtures.ProfileCircle();

            var scene = ToolPathSceneProjection.Build(BuildPath(drill, profile));

            Assert.IsTrue(scene.Shapes.All(s => s.Operation != null), "У фигуры есть операция");
            Assert.IsTrue(scene.Shapes.Any(s => ReferenceEquals(s.Operation, drill)), "Сверление на месте");
            Assert.IsTrue(scene.Shapes.Any(s => ReferenceEquals(s.Operation, profile)), "Контур на месте");
        }

        /// <summary>
        /// Погружение по глубине сверху выглядит точкой: рисовать по нему
        /// нечего, и лишних фигур оно давать не должно.
        /// </summary>
        [TestMethod]
        public void PlungeOnly_ProducesNoShape()
        {
            var operation = new Toolpath.ToolPathOperation("drill", "drill", 3, new DrillPointsOperation());
            var builder = new Toolpath.ToolPathBuilder(operation);
            builder.RapidTo(z: 5);
            builder.LinearTo(z: -2);

            var path = new Toolpath.ToolPath();
            path.AddOperation(operation);

            Assert.AreEqual(0, ToolPathSceneProjection.Build(path).Shapes.Count);
        }

        [TestMethod]
        public void EmptyToolPath_GivesEmptyScene()
        {
            Assert.IsTrue(ToolPathSceneProjection.Build(null).IsEmpty);
            Assert.IsTrue(ToolPathSceneProjection.Build(new Toolpath.ToolPath()).IsEmpty);
        }

        /// <summary>
        /// Пока программа не построена, показывать траекторию нечем, и
        /// предпросмотр остаётся на контурах.
        /// </summary>
        [TestMethod]
        public void PreviewViewModel_FallsBackToContoursWithoutToolPath()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(OperationFixtures.PocketCircle());
            var preview = main.OperationsWorkspace.OperationsPreview;

            Assert.IsFalse(preview.HasToolPath, "Траектории ещё нет");

            preview.ShowToolPath = true;

            Assert.IsTrue(preview.Scene.Shapes.All(s => s.Kind == OperationShapeKind.Contour),
                "Без траектории показываются контуры");
        }

        /// <summary>
        /// Траектория строится по слепку документа — клонам операций, — но
        /// фигуры сцены обязаны вести к операциям самого документа. Прежде
        /// клик по траектории выбирал клон, которого нет в списке: выделение
        /// списка снималось, перестановка гасла, «удалить» молча ничего
        /// не удаляла, а правки по двойному щелчку уходили в отсоединённый
        /// клон и пропадали. Тест идёт через настоящую генерацию — путь,
        /// который прежние тесты предпросмотра обходили, строя траекторию
        /// прямо из живых операций.
        /// </summary>
        [TestMethod]
        public async Task ToolPathScene_AfterRealGeneration_LeadsToDocumentOperations()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var pocket = OperationFixtures.PocketCircle();
            main.OperationsWorkspace.AllOperations.Add(pocket);

            await ((IAsyncRelayCommand)main.GCodeWorkflow.GenerateGCodeCommand).ExecuteAsync(null);
            Assert.IsNotNull(main.GCodeWorkflow.GeneratedToolPath, "генерация должна была пройти");

            var preview = main.OperationsWorkspace.OperationsPreview;
            preview.ShowToolPath = true;

            Assert.IsTrue(preview.Scene.Shapes.Count > 0, "сцена траектории не пуста");
            Assert.IsTrue(preview.Scene.Shapes.All(s => ReferenceEquals(s.Operation, pocket)),
                "фигуры траектории ведут к операции документа, а не к её клону из слепка");

            // Клик по траектории выбирает операцию документа: команды списка —
            // перестановка, удаление, правка — снова получают знакомый объект.
            preview.SelectedOperation = preview.Scene.Shapes[0].Operation;
            Assert.AreSame(pocket, main.OperationsWorkspace.SelectedOperation);
        }

        [TestMethod]
        public void PreviewViewModel_SwitchesBetweenContoursAndToolPath()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var pocket = OperationFixtures.PocketCircle();
            main.OperationsWorkspace.AllOperations.Add(pocket);

            var preview = main.OperationsWorkspace.OperationsPreview;
            preview.ToolPath = BuildPath(pocket);

            Assert.IsTrue(preview.HasToolPath, "Траектория получена");
            Assert.IsTrue(preview.Scene.Shapes.All(s => s.Kind == OperationShapeKind.Contour),
                "По умолчанию показываются контуры");

            preview.ShowToolPath = true;
            Assert.IsTrue(preview.Scene.Shapes.Any(s => s.Kind == OperationShapeKind.CuttingMove),
                "Переключение показывает траекторию");

            preview.ShowToolPath = false;
            Assert.IsTrue(preview.Scene.Shapes.All(s => s.Kind == OperationShapeKind.Contour),
                "Обратное переключение возвращает контуры");
        }
    }
}
