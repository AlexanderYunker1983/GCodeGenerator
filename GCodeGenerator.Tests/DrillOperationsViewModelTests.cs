using System;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels.Drill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты DrillOperationsViewModel (пункт 3.4 плана): диалог редактирования
    /// выбирается по DrillMode операции, а не по её имени.
    /// </summary>
    [TestClass]
    public class DrillOperationsViewModelTests
    {
        /// <summary>Заглушка диалоговой VM: фиксирует, что EditSelectedOperation передал операцию.</summary>
        private sealed class StubDrillDialogVm : IDrillDialogViewModel
        {
            public DrillOperationsViewModel MainViewModel { get; set; }
            public DrillPointsOperation Operation { get; set; }
        }

        /// <summary>Фиксирует вызовы IDialogService без показа окон.</summary>
        private sealed class RecordingDialogService : IDialogService
        {
            public Type CreatedType { get; private set; }
            public Type ShownType { get; private set; }
            public object ShownVm { get; private set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "") { }
            public bool ShowConfirm(string message, string title = "") => true;
            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => null;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "") => null;

            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class
                => throw new NotSupportedException("в тесте используется CreateViewModel(Type)");

            public object CreateViewModel(Type viewModelType)
            {
                CreatedType = viewModelType;
                return new StubDrillDialogVm();
            }

            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
                => throw new NotSupportedException("в тесте используется ShowDialog(Type, object)");

            public void ShowDialog(Type viewModelType, object viewModel)
            {
                ShownType = viewModelType;
                ShownVm = viewModel;
            }
        }

        [TestMethod]
        public void GetDialogViewModelType_AllModes_MappedCorrectly()
        {
            var vm = new DrillOperationsViewModel(null, null);

            Assert.AreEqual(typeof(DrillPointsOperationViewModel), vm.GetDialogViewModelType(DrillMode.Points));
            Assert.AreEqual(typeof(DrillLineOperationViewModel), vm.GetDialogViewModelType(DrillMode.Line));
            Assert.AreEqual(typeof(DrillArrayOperationViewModel), vm.GetDialogViewModelType(DrillMode.Array));
            Assert.AreEqual(typeof(DrillRectOperationViewModel), vm.GetDialogViewModelType(DrillMode.Rect));
            Assert.AreEqual(typeof(DrillCircleOperationViewModel), vm.GetDialogViewModelType(DrillMode.Circle));
            Assert.AreEqual(typeof(DrillArcOperationViewModel), vm.GetDialogViewModelType(DrillMode.Arc));
            Assert.AreEqual(typeof(DrillPolygonOperationViewModel), vm.GetDialogViewModelType(DrillMode.Polygon));
            Assert.AreEqual(typeof(DrillEllipseOperationViewModel), vm.GetDialogViewModelType(DrillMode.Ellipse));
            Assert.AreEqual(typeof(DrillPackageOperationViewModel), vm.GetDialogViewModelType(DrillMode.Package));
        }

        /// <summary>
        /// Сценарий из плана: переименованная операция открывает верный диалог
        /// (ранее name-based dispatch при переименовании открывал Points-диалог).
        /// </summary>
        [TestMethod]
        public void EditSelectedOperation_RenamedOperation_OpensDialogByMode()
        {
            var dialogService = new RecordingDialogService();
            var vm = new DrillOperationsViewModel(null, dialogService);

            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Arc,
                Name = "Переименованная операция"
            };
            vm.Operations.Add(op);
            vm.SelectedOperation = op;

            vm.EditSelectedOperation();

            Assert.AreEqual(typeof(DrillArcOperationViewModel), dialogService.CreatedType,
                "Диалог выбирается по DrillMode, а не по имени");
            Assert.AreEqual(typeof(DrillArcOperationViewModel), dialogService.ShownType);
            Assert.AreSame(op, ((IDrillDialogViewModel)dialogService.ShownVm).Operation,
                "В диалог передана та же операция");
        }

        [TestMethod]
        public void EditSelectedOperation_EachMode_OpensMatchingDialog()
        {
            var cases = new[]
            {
                (DrillMode.Points, typeof(DrillPointsOperationViewModel)),
                (DrillMode.Line, typeof(DrillLineOperationViewModel)),
                (DrillMode.Array, typeof(DrillArrayOperationViewModel)),
                (DrillMode.Rect, typeof(DrillRectOperationViewModel)),
                (DrillMode.Circle, typeof(DrillCircleOperationViewModel)),
                (DrillMode.Arc, typeof(DrillArcOperationViewModel)),
                (DrillMode.Polygon, typeof(DrillPolygonOperationViewModel)),
                (DrillMode.Ellipse, typeof(DrillEllipseOperationViewModel)),
                (DrillMode.Package, typeof(DrillPackageOperationViewModel))
            };

            foreach (var (mode, expectedType) in cases)
            {
                var dialogService = new RecordingDialogService();
                var vm = new DrillOperationsViewModel(null, dialogService);
                var op = new DrillPointsOperation { DrillMode = mode, Name = "Имя" };
                vm.Operations.Add(op);
                vm.SelectedOperation = op;

                vm.EditSelectedOperation();

                Assert.AreEqual(expectedType, dialogService.CreatedType, $"mode={mode}");
            }
        }
    }
}
