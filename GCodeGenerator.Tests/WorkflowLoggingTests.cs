using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.GCodeGenerators;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Рабочие процессы генерации и проекта сообщают в журнал об успехе и сбое:
    /// без этого единственным следом ошибки оставалось модальное окно, которое
    /// пользователь закрывал вместе с текстом исключения.
    /// </summary>
    [TestClass]
    public class WorkflowLoggingTests
    {
        private sealed class RecordingLogger : IAppLogger
        {
            public List<(LogLevel Level, string Message, Exception Exception)> Records { get; }
                = new List<(LogLevel, string, Exception)>();

            public void Log(LogLevel level, string message, Exception exception = null)
                => Records.Add((level, message, exception));

            public bool Has(LogLevel level, string fragment)
                => Records.Any(r => r.Level == level && r.Message != null && r.Message.Contains(fragment));
        }

        private sealed class ThrowingGenerator : IGCodeGenerator
        {
            public GCodeProgram Generate(IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
                => throw new InvalidOperationException("generator failure");

            public GCodeGenerator.Toolpath.ToolPath BuildToolPath(
                IList<OperationBase> operations, GCodeSettings settings, IProgress<int> progress = null)
                => throw new InvalidOperationException("generator failure");
        }

        private sealed class SilentDialogService : IDialogService
        {
            public string SaveDialogResult { get; set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "") { }
            public bool ShowConfirm(string message, string title = "") => true;
            public SaveConfirmation ShowSaveConfirmation(string message, string title = "") => SaveConfirmation.Discard;
            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => null;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "")
                => SaveDialogResult;
            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class => throw new NotSupportedException();
            public object CreateViewModel(Type viewModelType) => throw new NotSupportedException();
            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class => throw new NotSupportedException();
            public void ShowDialog(Type viewModelType, object viewModel) => throw new NotSupportedException();
        }

        private static ObservableCollection<OperationBase> OneDrillOperation()
            => new ObservableCollection<OperationBase>
            {
                new DrillPointsOperation
                {
                    Name = "Drill",
                    Holes = { new DrillHole { X = 1, Y = 2, Z = 0, TotalDepth = 2, StepDepth = 1 } }
                }

            };

        [TestMethod]
        public async Task Generation_Success_LogsOperationAndLineCount()
        {
            var logger = new RecordingLogger();
            var factory = new GCodeWorkflowFactory(
                new SimpleGCodeGenerator(),
                null,
                new SilentDialogService(),
                new GCodeFileService(),
                logger);
            var workflow = factory.Create(OneDrillOperation(), new GCodeSettings());

            await ((AsyncRelayCommand)workflow.GenerateGCodeCommand).ExecuteAsync(null);

            Assert.IsTrue(logger.Has(LogLevel.Info, "G-code generated"), "Успешная генерация должна попасть в журнал");
            Assert.IsTrue(logger.Has(LogLevel.Info, "1 operation(s)"), "В журнале должно быть число операций");
        }

        [TestMethod]
        public async Task Generation_Failure_LogsErrorWithException()
        {
            var logger = new RecordingLogger();
            var factory = new GCodeWorkflowFactory(
                new ThrowingGenerator(),
                null,
                new SilentDialogService(),
                new GCodeFileService(),
                logger);
            var workflow = factory.Create(OneDrillOperation(), new GCodeSettings());

            await ((AsyncRelayCommand)workflow.GenerateGCodeCommand).ExecuteAsync(null);

            var error = logger.Records.SingleOrDefault(r => r.Level == LogLevel.Error);
            Assert.IsNotNull(error.Message, "Сбой генерации должен попасть в журнал");
            StringAssert.Contains(error.Message, "G-code generation failed");
            Assert.IsInstanceOfType(error.Exception, typeof(InvalidOperationException));
        }

        [TestMethod]
        public void ProjectSave_Failure_LogsErrorWithPath()
        {
            var logger = new RecordingLogger();
            // Путь с недопустимым для файловой системы именем: сохранение падает
            // на уровне службы файлов проекта.
            var invalidPath = "?:\\<>|\\project.ygc";
            var dialogService = new SilentDialogService { SaveDialogResult = invalidPath };
            var factory = new ProjectWorkflowFactory(
                null,
                dialogService,
                new DefaultSettingsStore(),
                new ProjectFileService(),
                logger);
            var gCodeFactory = new GCodeWorkflowFactory(
                new SimpleGCodeGenerator(),
                null,
                dialogService,
                new GCodeFileService(),
                logger);
            var operations = OneDrillOperation();
            var workflow = factory.Create(operations, gCodeFactory.Create(operations, new GCodeSettings()));

            workflow.SaveProjectCommand.Execute(null);

            var error = logger.Records.SingleOrDefault(r => r.Level == LogLevel.Error);
            Assert.IsNotNull(error.Message, "Сбой сохранения проекта должен попасть в журнал");
            StringAssert.Contains(error.Message, "Saving project failed");
            StringAssert.Contains(error.Message, "project.ygc");
        }

        /// <summary>Хранилище настроек по умолчанию, без персистентности.</summary>
        private sealed class DefaultSettingsStore : ISettingsStore
        {
            public event EventHandler SettingsChanged;

            public GCodeSettings Current { get; } = new GCodeSettings();

            public void Save() => SettingsChanged?.Invoke(this, EventArgs.Empty);

            public void RestoreGlobalGenerationSettings() => SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
