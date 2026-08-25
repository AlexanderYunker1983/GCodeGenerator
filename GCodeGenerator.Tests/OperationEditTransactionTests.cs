using System;
using System.Collections.ObjectModel;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class OperationEditTransactionTests
    {
        [TestMethod]
        public void DrillPoints_Cancel_DoesNotChangeScalarsOrNestedHoles()
        {
            var operation = new DrillPointsOperation
            {
                FeedXYRapid = 1000,
                Holes = { new DrillHole { X = 10, Y = 20, TotalDepth = 2, StepDepth = 1 } },
            };
            var operations = new ObservableCollection<OperationBase> { operation };
            var dialogs = CreateDialogs(
                _ => new DrillPointsOperationViewModel(null),
                vm =>
                {
                    var editor = (DrillPointsOperationViewModel)vm;
                    editor.Operation.FeedXYRapid = 9999;
                    editor.Operation.Holes[0].X = 777;
                    editor.CancelCommand.Execute(null);
                });

            new OperationEditorFactory(dialogs).ShowEditor(operation, operations);

            Assert.AreEqual(1000, operation.FeedXYRapid);
            Assert.AreEqual(10, operation.Holes[0].X);
        }

        [TestMethod]
        public void ProfileDxf_Cancel_DoesNotChangePassThroughFieldsOrGeometry()
        {
            var originalPolyline = Poly((0, 0), (10, 0));
            var operation = new ProfileDxfOperation
            {
                ToolDiameter = 3,
                DxfFilePath = "original.dxf",
                Polylines = { originalPolyline },
            };
            var operations = new ObservableCollection<OperationBase> { operation };
            var dialogs = CreateDialogs(
                _ => new ProfileDxfOperationViewModel(null, null, new DxfImportService()),
                vm =>
                {
                    var editor = (ProfileDxfOperationViewModel)vm;
                    editor.Operation.ToolDiameter = 12;
                    editor.Operation.DxfFilePath = "changed.dxf";
                    editor.Operation.Polylines = new System.Collections.Generic.List<Polyline2D>
                    {
                        Poly((1, 1), (2, 2)),
                    };
                    editor.CancelCommand.Execute(null);
                });

            new OperationEditorFactory(dialogs).ShowEditor(operation, operations);

            Assert.AreEqual(3, operation.ToolDiameter);
            Assert.AreEqual("original.dxf", operation.DxfFilePath);
            Assert.AreSame(originalPolyline, operation.Polylines[0]);
        }

        [TestMethod]
        public void ProfileCircle_Ok_CommitsWorkingCopyAndPreservesOperationIdentity()
        {
            var operation = new ProfileCircleOperation { Radius = 10 };
            var operations = new ObservableCollection<OperationBase> { operation };
            int notifications = 0;
            operation.PropertyChanged += (_, _) => notifications++;
            var dialogs = CreateDialogs(
                _ => new ProfileCircleOperationViewModel(null),
                vm =>
                {
                    var editor = (ProfileCircleOperationViewModel)vm;
                    editor.Operation.Radius = 25;
                    editor.OkCommand.Execute(null);
                });

            new OperationEditorFactory(dialogs).ShowEditor(operation, operations);

            Assert.AreSame(operation, operations[0]);
            Assert.AreEqual(25, operation.Radius);
            Assert.IsTrue(notifications > 0, "Коммит должен уведомить MainViewModel об изменении содержимого");
        }

        [TestMethod]
        public void DialogClosedWithoutOk_DiscardsWorkingCopy()
        {
            var operation = new ProfileCircleOperation { Radius = 10 };
            var operations = new ObservableCollection<OperationBase> { operation };
            var dialogs = CreateDialogs(
                _ => new ProfileCircleOperationViewModel(null),
                vm => ((ProfileCircleOperationViewModel)vm).Operation.Radius = 99);

            new OperationEditorFactory(dialogs).ShowEditor(operation, operations);

            Assert.AreEqual(10, operation.Radius);
        }

        /// <summary>
        /// Ошибка в параметрах не должна стоить пользователю операции:
        /// диалог остаётся открытым, а исходная операция — в списке
        /// со своими прежними значениями.
        /// </summary>
        [TestMethod]
        public void InvalidOk_KeepsOriginalOperation()
        {
            var operation = new DrillPointsOperation
            {
                Holes = { new DrillHole { X = 1, Y = 2, TotalDepth = 2, StepDepth = 1 } },
            };
            var operations = new ObservableCollection<OperationBase> { operation };
            var dialogs = CreateDialogs(
                _ => new DrillPointsOperationViewModel(null),
                vm =>
                {
                    var editor = (DrillPointsOperationViewModel)vm;
                    editor.Operation.Holes.Clear();
                    editor.OkCommand.Execute(null);
                });

            new OperationEditorFactory(dialogs).ShowEditor(operation, operations);

            Assert.AreEqual(1, operations.Count, "операция остаётся в списке");
            Assert.AreSame(operation, operations[0], "остаётся именно исходная операция");
            Assert.AreEqual(1, operation.Holes.Count, "отверстия исходной операции не тронуты");
        }

        private static MainViewModelOperationEditTests.RecordingDialogService CreateDialogs(
            Func<Type, object> factory,
            Action<object> action)
        {
            return new MainViewModelOperationEditTests.RecordingDialogService
            {
                ViewModelFactory = factory,
                DialogAction = action,
            };
        }

        private static Polyline2D Poly(params (double x, double y)[] points)
        {
            var polyline = new Polyline2D();
            foreach (var point in points)
                polyline.Points.Add(new Point2D { X = point.x, Y = point.y });
            return polyline;
        }
    }
}
