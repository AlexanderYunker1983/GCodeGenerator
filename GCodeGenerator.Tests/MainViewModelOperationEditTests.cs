using System;
using System.Collections.ObjectModel;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Тесты редактирования операций (пункты 3.4, 7.2, 7.3 плана): диалог
    /// выбирается фабрикой IOperationEditorFactory по типу операции (сверление —
    /// по DrillMode, а не по имени); диалог получает единую коллекцию
    /// AllOperations.
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

        private static (MainViewModel main, OperationEditorFactory factory, RecordingDialogService dialogService) CreateMain()
        {
            var dialogService = new RecordingDialogService();
            var factory = new OperationEditorFactory(dialogService);
            // Пункт 7.5 плана: версия/настройки/тема — через IoC (в тесте — фиксы).
            // Пункт 7.6 плана: IProjectFileService — в тесте реальный класс (без состояния).
            var main = new MainViewModel(null, dialogService, new SimpleGCodeGenerator(), factory,
                new ProgramInfo("1.0"), new FakeSettingsStore(), new FakeThemeService(), new ProjectFileService());
            return (main, factory, dialogService);
        }

        /// <summary>Фикс ISettingsStore (пункт 7.5 плана): настройки по умолчанию, без персистентности.</summary>
        private sealed class FakeSettingsStore : ISettingsStore
        {
            public GCodeSettings Current { get; } = new GCodeSettings();
            public void Save() { }
        }

        /// <summary>Фикс IThemeService (пункт 7.5 плана): без WPF.</summary>
        private sealed class FakeThemeService : IThemeService
        {
            public event EventHandler ThemeChanged;
            public void ApplyTheme(bool useDarkTheme) => ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        [TestMethod]
        public void GetViewModelType_AllDrillModes_MappedCorrectly()
        {
            var (_, factory, _) = CreateMain();

            Assert.AreEqual(typeof(DrillPointsOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Points }));
            Assert.AreEqual(typeof(DrillLineOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Line }));
            Assert.AreEqual(typeof(DrillArrayOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Array }));
            Assert.AreEqual(typeof(DrillRectOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Rect }));
            Assert.AreEqual(typeof(DrillCircleOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Circle }));
            Assert.AreEqual(typeof(DrillArcOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Arc }));
            Assert.AreEqual(typeof(DrillPolygonOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Polygon }));
            Assert.AreEqual(typeof(DrillEllipseOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Ellipse }));
            Assert.AreEqual(typeof(DrillPackageOperationViewModel), factory.GetViewModelType(new DrillPointsOperation { DrillMode = DrillMode.Package }));
        }

        [TestMethod]
        public void GetViewModelType_NonDrill_MappedCorrectly()
        {
            var (_, factory, _) = CreateMain();

            Assert.AreEqual(typeof(PocketCircleOperationViewModel), factory.GetViewModelType(new PocketCircleOperation()));
            Assert.AreEqual(typeof(PocketRectangleOperationViewModel), factory.GetViewModelType(new PocketRectangleOperation()));
            Assert.AreEqual(typeof(PocketEllipseOperationViewModel), factory.GetViewModelType(new PocketEllipseOperation()));
            Assert.AreEqual(typeof(PocketDxfOperationViewModel), factory.GetViewModelType(new PocketDxfOperation()));
            Assert.AreEqual(typeof(ProfileCircleOperationViewModel), factory.GetViewModelType(new ProfileCircleOperation()));
            Assert.AreEqual(typeof(ProfileRectangleOperationViewModel), factory.GetViewModelType(new ProfileRectangleOperation()));
            Assert.AreEqual(typeof(ProfileRoundedRectangleOperationViewModel), factory.GetViewModelType(new ProfileRoundedRectangleOperation()));
            Assert.AreEqual(typeof(ProfileEllipseOperationViewModel), factory.GetViewModelType(new ProfileEllipseOperation()));
            Assert.AreEqual(typeof(ProfilePolygonOperationViewModel), factory.GetViewModelType(new ProfilePolygonOperation()));
            Assert.AreEqual(typeof(ProfileDxfOperationViewModel), factory.GetViewModelType(new ProfileDxfOperation()));
        }

        /// <summary>
        /// Сценарий из плана: переименованная операция открывает верный диалог
        /// (ранее name-based dispatch при переименовании открывал Points-диалог).
        /// </summary>
        [TestMethod]
        public void EditSelectedOperation_RenamedOperation_OpensDialogByMode()
        {
            var (main, _, dialogService) = CreateMain();

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
                var (main, _, dialogService) = CreateMain();
                var op = new DrillPointsOperation { DrillMode = mode, Name = "Имя" };
                main.AllOperations.Add(op);
                main.SelectedOperation = op;

                main.EditOperationCommand.Execute(null);

                Assert.AreEqual(expectedType, dialogService.CreatedType, $"mode={mode}");
            }
        }
    }
}
