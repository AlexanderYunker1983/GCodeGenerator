using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Preview;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Регрессия пункта 7.2 плана: сохранение из диалога (OK) и импорт DXF
    /// обязаны пересобирать сцену 2D-превью. Геометрия операций — авто-свойства
    /// (без PropertyChanged), поэтому пересборка срабатывала только при следующем
    /// изменении коллекции: отредактированная операция оставалась невидимой/устаревшей
    /// на 2D-превью до добавления следующей операции.
    /// </summary>
    [TestClass]
    public class SceneRebuildOnEditTests
    {
        /// <summary>Заглушка IDialogService: «выбирает» DXF-файл для импорта (без окон).</summary>
        private sealed class StubDialogService : IDialogService
        {
            public string OpenDialogResult { get; set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "") { }
            public bool ShowConfirm(string message, string title = "") => true;
            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => OpenDialogResult;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "") => null;
            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class => throw new NotSupportedException();
            public object CreateViewModel(Type viewModelType) => throw new NotSupportedException();
            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class => throw new NotSupportedException();
            public void ShowDialog(Type viewModelType, object viewModel) => throw new NotSupportedException();
        }

        [TestMethod]
        public void DialogOk_DrillOperation_SceneRebuilt()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var op = new DrillPointsOperation();
            main.AllOperations.Add(op);

            var sceneBefore = main.OperationsPreview.Scene;
            Assert.AreEqual(0, sceneBefore.Shapes.Count, "У нового сверления ещё нет отверстий");

            // Диалог: LoadFromOperation создаёт отверстие по умолчанию (0,0); OK сохраняет его.
            var dlg = new DrillPointsOperationViewModel(null);
            dlg.Operation = op;
            ((RelayCommand)dlg.OkCommand).Execute(null);

            var sceneAfter = main.OperationsPreview.Scene;
            Assert.IsFalse(ReferenceEquals(sceneBefore, sceneAfter), "Сцена должна пересобраться после OK");
            Assert.IsTrue(sceneAfter.Shapes.Any(s => ReferenceEquals(s.Operation, op)
                && s.Kind == OperationShapeKind.Point), "Точка отверстия должна быть в сцене");
        }

        [TestMethod]
        public void DialogOk_ProfileOperation_SceneRebuiltWithNewGeometry()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var op = new ProfileCircleOperation();
            main.AllOperations.Add(op);

            var sceneBefore = main.OperationsPreview.Scene;

            // Диалог: меняем радиус (по умолчанию 10) и сохраняем.
            var dlg = new ProfileCircleOperationViewModel(null);
            dlg.Operation = op;
            dlg.Radius = 25;
            ((RelayCommand)dlg.OkCommand).Execute(null);

            var sceneAfter = main.OperationsPreview.Scene;
            Assert.IsFalse(ReferenceEquals(sceneBefore, sceneAfter), "Сцена должна пересобраться после OK");

            // Контур в сцене — с новым радиусом (25), а не со значением по умолчанию (10).
            var shape = sceneAfter.Shapes.Single(s => ReferenceEquals(s.Operation, op));
            var maxRadius = shape.Points.Max(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
            Assert.AreEqual(25, maxRadius, 1e-3, "Контур должен отражать сохранённый радиус");
        }

        [TestMethod]
        public async Task DxfImport_ProfileOperation_SceneRebuilt()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var op = new ProfileDxfOperation();
            main.AllOperations.Add(op);

            var sceneBefore = main.OperationsPreview.Scene;
            Assert.AreEqual(0, sceneBefore.Shapes.Count, "У нового DXF-профиля ещё нет контуров");

            var dialogService = new StubDialogService
            {
                OpenDialogResult = DxfFixtureLoader.GetAssetPath("profile_sample.dxf")
            };
            var vm = new ProfileDxfOperationViewModel(null, dialogService, new DxfImportService());
            vm.Operation = op;
            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            var sceneAfter = main.OperationsPreview.Scene;
            Assert.IsFalse(ReferenceEquals(sceneBefore, sceneAfter), "Сцена должна пересобраться после импорта DXF");
            Assert.IsTrue(sceneAfter.Shapes.Any(s => ReferenceEquals(s.Operation, op)
                && s.Kind == OperationShapeKind.Contour), "Контур DXF должен быть в сцене");
        }

        [TestMethod]
        public async Task DxfImport_PocketOperation_SceneRebuilt()
        {
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();
            var op = new PocketDxfOperation();
            main.AllOperations.Add(op);

            var sceneBefore = main.OperationsPreview.Scene;
            Assert.AreEqual(0, sceneBefore.Shapes.Count, "У нового DXF-кармана ещё нет контуров");

            var dialogService = new StubDialogService
            {
                OpenDialogResult = DxfFixtureLoader.GetAssetPath("pocket_sample.dxf")
            };
            var vm = new PocketDxfOperationViewModel(null, dialogService, new DxfImportService());
            vm.Operation = op;
            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            var sceneAfter = main.OperationsPreview.Scene;
            Assert.IsFalse(ReferenceEquals(sceneBefore, sceneAfter), "Сцена должна пересобраться после импорта DXF");
            Assert.IsTrue(sceneAfter.Shapes.Any(s => ReferenceEquals(s.Operation, op)
                && s.Kind == OperationShapeKind.Contour), "Контур DXF должен быть в сцене");
        }
    }
}
