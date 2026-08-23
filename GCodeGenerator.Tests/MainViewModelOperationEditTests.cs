using System;
using System.Collections.ObjectModel;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Drill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты редактирования операций в MainViewModel (пункты 3.4 и 7.2 плана):
    /// диалог редактирования сверления выбирается по DrillMode операции, а не
    /// по её имени; пункт 7.2: диалог получает единую коллекцию AllOperations.
    /// </summary>
    [TestClass]
    public class MainViewModelOperationEditTests
    {
        /// <summary>Заглушка диалоговой VM: фиксирует операцию и коллекцию, переданные в диалог.</summary>
        private sealed class StubDrillDialogVm : IDrillDialogViewModel
        {
            public ObservableCollection<OperationBase> Operations { get; set; }
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

        private static MainViewModel CreateMain(IDialogService dialogService)
            => new MainViewModel(null, dialogService, new SimpleGCodeGenerator());

        [TestMethod]
        public void GetDialogViewModelType_AllModes_MappedCorrectly()
        {
            var main = CreateMain(new RecordingDialogService());

            Assert.AreEqual(typeof(DrillPointsOperationViewModel), main.GetDialogViewModelType(DrillMode.Points));
            Assert.AreEqual(typeof(DrillLineOperationViewModel), main.GetDialogViewModelType(DrillMode.Line));
            Assert.AreEqual(typeof(DrillArrayOperationViewModel), main.GetDialogViewModelType(DrillMode.Array));
            Assert.AreEqual(typeof(DrillRectOperationViewModel), main.GetDialogViewModelType(DrillMode.Rect));
            Assert.AreEqual(typeof(DrillCircleOperationViewModel), main.GetDialogViewModelType(DrillMode.Circle));
            Assert.AreEqual(typeof(DrillArcOperationViewModel), main.GetDialogViewModelType(DrillMode.Arc));
            Assert.AreEqual(typeof(DrillPolygonOperationViewModel), main.GetDialogViewModelType(DrillMode.Polygon));
            Assert.AreEqual(typeof(DrillEllipseOperationViewModel), main.GetDialogViewModelType(DrillMode.Ellipse));
            Assert.AreEqual(typeof(DrillPackageOperationViewModel), main.GetDialogViewModelType(DrillMode.Package));
        }

        /// <summary>
        /// Сценарий из плана: переименованная операция открывает верный диалог
        /// (ранее name-based dispatch при переименовании открывал Points-диалог).
        /// </summary>
        [TestMethod]
        public void EditSelectedOperation_RenamedOperation_OpensDialogByMode()
        {
            var dialogService = new RecordingDialogService();
            var main = CreateMain(dialogService);

            var op = new DrillPointsOperation
            {
                DrillMode = DrillMode.Arc,
                Name = "Переименованная операция"
            };
            main.AllOperations.Add(op);
            main.SelectedOperation = op;

            main.EditOperationCommand.Execute(null);

            Assert.AreEqual(typeof(DrillArcOperationViewModel), dialogService.CreatedType,
                "Диалог выбирается по DrillMode, а не по имени");
            Assert.AreEqual(typeof(DrillArcOperationViewModel), dialogService.ShownType);
            Assert.AreSame(op, ((IDrillDialogViewModel)dialogService.ShownVm).Operation,
                "В диалог передана та же операция");
            Assert.AreSame(main.AllOperations, ((IDrillDialogViewModel)dialogService.ShownVm).Operations,
                "Диалог получает единую коллекцию операций (пункт 7.2)");
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
                var main = CreateMain(dialogService);
                var op = new DrillPointsOperation { DrillMode = mode, Name = "Имя" };
                main.AllOperations.Add(op);
                main.SelectedOperation = op;

                main.EditOperationCommand.Execute(null);

                Assert.AreEqual(expectedType, dialogService.CreatedType, $"mode={mode}");
            }
        }
    }
}
