using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels;
using GCodeGenerator.ViewModels.Drill;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

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
        private sealed class StubEditorViewModel : IOperationEditorViewModel
        {
            public ObservableCollection<OperationBase> Operations { private get; set; }

            public OperationBase EditedOperation { get; private set; }

            public bool IsAccepted => false;

            public bool IsRemovalRequested => false;

            public void SetOperation(OperationBase operation) => EditedOperation = operation;

            /// <summary>Коллекция, переданная диалогу (для проверок теста).</summary>
            public ObservableCollection<OperationBase> ReceivedOperations => Operations;
        }

        /// <summary>Фиксирует вызовы IDialogService без показа окон.</summary>
        /// Пункт 8.4: internal — переиспользуется в AsyncGenerationTests.
        internal sealed class RecordingDialogService : IDialogService
        {
            public Type CreatedType { get; private set; }
            public Type ShownType { get; private set; }
            public object ShownVm { get; private set; }
            public string LastErrorMessage { get; private set; }
            public string LastErrorTitle { get; private set; }
            public Func<Type, object> ViewModelFactory { get; set; }
            public Action<object> DialogAction { get; set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "")
            {
                LastErrorMessage = message;
                LastErrorTitle = title;
            }
            public bool ShowConfirm(string message, string title = "") => true;

            /// <summary>Пункт 8.2: путь, который «выбирает» диалог открытия (null — отмена).</summary>
            public string OpenDialogResult { get; set; }
            public string SaveDialogResult { get; set; }

            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => OpenDialogResult;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "") => SaveDialogResult;

            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class
                => throw new NotSupportedException("в тесте используется CreateViewModel(Type)");

            public object CreateViewModel(Type viewModelType)
            {
                CreatedType = viewModelType;
                return ViewModelFactory?.Invoke(viewModelType) ?? new StubEditorViewModel();
            }

            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
                => throw new NotSupportedException("в тесте используется ShowDialog(Type, object)");

            public void ShowDialog(Type viewModelType, object viewModel)
            {
                ShownType = viewModelType;
                ShownVm = viewModel;
                DialogAction?.Invoke(viewModel);
            }
        }

        /// <summary>
        /// Пункт 8.4: internal + опциональный генератор — переиспользуется в
        /// AsyncGenerationTests (медленный фикс-генератор для проверки async).
        /// </summary>
        internal static (MainViewModel Main, OperationEditorFactory Factory, RecordingDialogService DialogService, FakeSettingsStore SettingsStore) CreateMain(
            IGCodeGenerator generator = null,
            IGCodeFileService gCodeFileService = null,
            IProjectFileService projectFileService = null)
        {
            var dialogService = new RecordingDialogService();
            var factory = new OperationEditorFactory(dialogService);
            // Пункт 7.5 плана: версия/настройки/тема — через IoC (в тесте — фиксы).
            // Пункт 7.6 плана: IProjectFileService — в тесте реальный класс (без состояния).
            var settingsStore = new FakeSettingsStore();
            var gCodeWorkflowFactory = new GCodeWorkflowFactory(
                generator ?? new SimpleGCodeGenerator(),
                null,
                dialogService,
                gCodeFileService ?? new GCodeFileService());
            var projectWorkflowFactory = new ProjectWorkflowFactory(
                null,
                dialogService,
                settingsStore,
                projectFileService ?? new ProjectFileService());
            var operationsWorkspace = new OperationsWorkspaceViewModel(
                null,
                factory,
                new FakeThemeService());
            var main = new MainViewModel(null, dialogService, gCodeWorkflowFactory, projectWorkflowFactory,
                operationsWorkspace, new ProgramInfo("1.0"), settingsStore);
            return (main, factory, dialogService, settingsStore);
        }

        /// <summary>
        /// Фикс ISettingsStore (пункт 7.5 плана): настройки по умолчанию, без персистентности.
        /// «Глобальные» значения настроек генерации — значения по умолчанию.
        /// </summary>
        /// <summary>Пункт 8.4: internal — переиспользуется в AsyncGenerationTests.</summary>
        internal sealed class FakeSettingsStore : ISettingsStore
        {
            public event EventHandler SettingsChanged;

            public GCodeSettings Current { get; } = new GCodeSettings();
            public int RestoreCalls { get; private set; }

            public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);

            public void RestoreGlobalGenerationSettings()
            {
                RestoreCalls++;
                Current.Format = new GCodeFormatSettings();
                Current.Spindle = new SpindleSettings();
                Current.Coolant = new CoolantSettings();
                Current.WorkCoordinate = new WorkCoordinateSettings();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Фикс IThemeService (пункт 7.5 плана): без WPF.</summary>
        private sealed class FakeThemeService : IThemeService
        {
            public event EventHandler ThemeChanged;
            public void ApplyTheme(bool useDarkTheme) => ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Фабрика передаёт диалогу операцию и общий список через единый
        /// контракт, ничего не зная о его типе. Диалог, который этот контракт
        /// не реализует, откроется с ошибкой в руках пользователя, поэтому
        /// соответствие проверяется здесь — для каждого типа операции
        /// эталонного набора.
        /// </summary>
        [TestMethod]
        public void GetViewModelType_EveryOperation_ImplementsEditorContract()
        {
            var (_, factory, _, _) = CreateMain();

            foreach (var operation in ReferenceOperations.Build())
            {
                var viewModelType = factory.GetViewModelType(operation);
                Assert.IsNotNull(viewModelType, $"Для {operation.GetType().Name} должен быть диалог");
                Assert.IsTrue(
                    typeof(IOperationEditorViewModel).IsAssignableFrom(viewModelType),
                    $"{viewModelType.Name} должен реализовывать {nameof(IOperationEditorViewModel)}");
            }
        }

        [TestMethod]
        public void GetViewModelType_AllDrillModes_MappedCorrectly()
        {
            var (_, factory, _, _) = CreateMain();

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
            var (_, factory, _, _) = CreateMain();

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
            var (main, _, dialogService, _) = CreateMain();

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
            var shown = (StubEditorViewModel)dialogService.ShownVm;
            Assert.AreNotSame(op, shown.EditedOperation,
                "Диалог должен получать изолированную рабочую копию");
            Assert.AreEqual(op.DrillMode, ((DrillPointsOperation)shown.EditedOperation).DrillMode);
            Assert.AreSame(main.AllOperations, shown.ReceivedOperations,
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
                var (main, _, dialogService, _) = CreateMain();
                var op = new DrillPointsOperation { DrillMode = mode, Name = "Имя" };
                main.AllOperations.Add(op);
                main.SelectedOperation = op;

                main.EditOperationCommand.Execute(null);

                Assert.AreEqual(expectedType, dialogService.CreatedType, $"mode={mode}");
            }
        }

        // ------------------------------------------------------------------
        // Настройки генерации проекта в сессии
        // ------------------------------------------------------------------

        /// <summary>
        /// Открытие проекта: все настройки генерации подставляются в сессию,
        /// а настройка темы UI остаётся текущей.
        /// </summary>
        [TestMethod]
        public void OpenProject_FileWithSections_AllGenerationSettingsReplacedAndUiPreserved()
        {
            var (main, _, dialogService, store) = CreateMain();

            var settings = new GCodeSettings();
            settings.Format.UseLineNumbers = false;
            settings.Spindle.SpindleSpeedRpm = 8000;
            settings.Spindle.SpindleStartCommand = "M4";
            settings.Coolant.CoolantStartEnabled = false;
            settings.WorkCoordinate.SetWorkCoordinateSystem = true;
            settings.WorkCoordinate.WorkCoordinateSystem = "G57";
            settings.Ui.UseDarkTheme = false;
            store.Current.Ui.UseDarkTheme = true;

            var filePath = Path.Combine(Path.GetTempPath(), "gcg_open_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                new ProjectFileService().Save(filePath,
                    new List<OperationBase> { OperationFixtures.DrillPoints() }, settings);
                dialogService.OpenDialogResult = filePath;

                main.OpenProjectCommand.Execute(null);

                Assert.AreEqual(1, main.AllOperations.Count, "Операция из файла загружена");
                Assert.IsFalse(store.Current.Format.UseLineNumbers, "Формат из секции файла в сессии");
                Assert.AreEqual(8000, store.Current.Spindle.SpindleSpeedRpm, "Шпиндель из секции файла в сессии");
                Assert.AreEqual("M4", store.Current.Spindle.SpindleStartCommand);
                Assert.IsFalse(store.Current.Coolant.CoolantStartEnabled, "СОЖ из секции файла в сессии");
                Assert.AreEqual("G57", store.Current.WorkCoordinate.WorkCoordinateSystem,
                    "Рабочая система координат из секции файла в сессии");
                Assert.IsTrue(store.Current.Ui.UseDarkTheme, "Проект не должен менять тему UI");
            }
            finally
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        /// <summary>
        /// Открытие СТАРОГО проекта (без секций): сессия восстанавливается к
        /// глобальным настройкам (не наследует значения ранее открытого проекта).
        /// </summary>
        [TestMethod]
        public void OpenProject_OldFileWithoutSections_SessionRestoredToGlobal()
        {
            var (main, _, dialogService, store) = CreateMain();

            // Сессия «изменена ранее открытым проектом».
            store.Current.Spindle.SpindleSpeedRpm = 9999;
            store.Current.Coolant.CoolantStartEnabled = false;
            store.Current.Format.UseLineNumbers = false;
            store.Current.WorkCoordinate.WorkCoordinateSystem = "G59";
            store.Current.Ui.UseDarkTheme = true;

            var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference", "legacy_project_v1.ygc");
            Assert.IsTrue(File.Exists(legacyPath), "Нет эталонного легаси-файла");
            dialogService.OpenDialogResult = legacyPath;

            main.OpenProjectCommand.Execute(null);

            Assert.AreEqual(19, main.AllOperations.Count, "Операции из старого файла загружены");
            Assert.AreEqual(12000, store.Current.Spindle.SpindleSpeedRpm, "Старый файл → глобальный шпиндель (дефолт)");
            Assert.IsTrue(store.Current.Coolant.CoolantStartEnabled, "Старый файл → глобальный СОЖ (дефолт)");
            Assert.IsTrue(store.Current.Format.UseLineNumbers, "Старый файл → глобальный формат (дефолт)");
            Assert.AreEqual("G54", store.Current.WorkCoordinate.WorkCoordinateSystem,
                "Старый файл → глобальная система координат (дефолт)");
            Assert.IsTrue(store.Current.Ui.UseDarkTheme, "Открытие проекта не меняет тему UI");
        }

        [TestMethod]
        public void OpenProject_UnsupportedVersion_PreservesCurrentProject()
        {
            var (main, _, dialogService, _) = CreateMain();
            var existingOperation = OperationFixtures.DrillPoints();
            main.AllOperations.Add(existingOperation);

            var path = Path.Combine(Path.GetTempPath(), "gcg_future_" + Guid.NewGuid().ToString("N") + ".ygc");
            try
            {
                File.WriteAllText(path, "{\"version\":5,\"operations\":[]}");
                dialogService.OpenDialogResult = path;

                main.OpenProjectCommand.Execute(null);

                Assert.AreEqual(1, main.AllOperations.Count);
                Assert.AreSame(existingOperation, main.AllOperations[0],
                    "Неподдерживаемый файл не должен частично заменять текущий проект");
                Assert.IsFalse(string.IsNullOrEmpty(dialogService.LastErrorMessage));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Новый проект: все настройки генерации сбрасываются к глобальным,
        /// тема UI остаётся текущей.
        /// </summary>
        [TestMethod]
        public void NewProgram_AllGenerationSettingsRestoredToGlobalAndUiPreserved()
        {
            var (main, _, _, store) = CreateMain();

            main.AllOperations.Add(OperationFixtures.DrillPoints());
            store.Current.Spindle.SpindleSpeedRpm = 9999;
            store.Current.Coolant.CoolantStartEnabled = false;
            store.Current.Format.UseLineNumbers = false;
            store.Current.WorkCoordinate.WorkCoordinateSystem = "G59";
            store.Current.Ui.UseDarkTheme = true;

            main.NewProgramCommand.Execute(null);

            Assert.AreEqual(0, main.AllOperations.Count, "Операции очищены");
            Assert.AreEqual(12000, store.Current.Spindle.SpindleSpeedRpm, "Новый проект → глобальный шпиндель");
            Assert.IsTrue(store.Current.Coolant.CoolantStartEnabled, "Новый проект → глобальный СОЖ");
            Assert.IsTrue(store.Current.Format.UseLineNumbers, "Новый проект → глобальный формат");
            Assert.AreEqual("G54", store.Current.WorkCoordinate.WorkCoordinateSystem,
                "Новый проект → глобальная система координат");
            Assert.IsTrue(store.Current.Ui.UseDarkTheme, "Новый проект не должен менять тему UI");
            Assert.IsTrue(store.RestoreCalls >= 1, "RestoreGlobalGenerationSettings вызван");
        }
    }
}
