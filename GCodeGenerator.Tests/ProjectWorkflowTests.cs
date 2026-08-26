using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class ProjectWorkflowTests
    {
        private sealed class RecordingProjectFileService : IProjectFileService
        {
            public string FilePath { get; private set; }
            public IReadOnlyList<OperationBase> Operations { get; private set; }
            public GCodeSettings Settings { get; private set; }

            public void Save(
                string filePath,
                IReadOnlyList<OperationBase> operations,
                GCodeSettings settings)
                => SaveSerialized(filePath, Serialize(operations, settings));

            // Слепок снимает Serialize — именно ему рабочий процесс отдаёт
            // операции и настройки перед уходом записи в фон.
            public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
            {
                Operations = operations;
                Settings = settings;
                return "serialized-project";
            }

            public void SaveSerialized(string filePath, string json)
                => FilePath = filePath;

            public ProjectFileData Load(string filePath)
                => throw new System.NotSupportedException();
        }

        private sealed class VersionedProjectFileService : IProjectFileService
        {
            public int LoadVersion { get; set; } = 2;

            public int SaveCount { get; private set; }

            public void Save(
                string filePath,
                IReadOnlyList<OperationBase> operations,
                GCodeSettings settings)
                => SaveSerialized(filePath, Serialize(operations, settings));

            public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => string.Empty;

            public void SaveSerialized(string filePath, string json)
                => SaveCount++;

            public ProjectFileData Load(string filePath)
                => new ProjectFileData
                {
                    Version = LoadVersion,
                    Operations = new List<OperationBase> { new DrillPointsOperation() },
                };
        }

        private static Task ExecuteAsync(System.Windows.Input.ICommand command)
            => ((IAsyncRelayCommand)command).ExecuteAsync(null);

        /// <summary>
        /// Файл старого формата после сохранения молча становился файлом
        /// текущей версии — прежние сборки его больше не откроют, и раньше
        /// пользователь узнавал об этом только там, где файл уже не открылся.
        /// Теперь сохранение сообщает об апгрейде, и ровно один раз: после
        /// него файл уже текущей версии.
        /// </summary>
        [TestMethod]
        public async Task SavingProjectLoadedFromOlderFormat_WarnsAboutUpgradeOnce()
        {
            var projectFiles = new VersionedProjectFileService { LoadVersion = 2 };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            dialogs.OpenDialogResult = "old-project.ygc";

            await ExecuteAsync(main.ProjectWorkflow.OpenProjectCommand);
            Assert.AreEqual(0, dialogs.InfoMessageCount, "открытие само по себе ничего не сообщает");

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);
            Assert.AreEqual(1, dialogs.InfoMessageCount, "первое сохранение сообщает об апгрейде формата");
            Assert.AreEqual("ProjectUpgradedFromOlderVersionInfo", dialogs.LastInfoMessage,
                "текст сообщения берётся из словаря локализации");

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);
            Assert.AreEqual(1, dialogs.InfoMessageCount, "файл уже текущей версии — повторного сообщения нет");
            Assert.AreEqual(2, projectFiles.SaveCount, "оба сохранения при этом состоялись");
        }

        /// <summary>Файл текущей версии сохраняется без сообщений.</summary>
        [TestMethod]
        public async Task SavingProjectLoadedFromCurrentFormat_StaysSilent()
        {
            var projectFiles = new VersionedProjectFileService
            {
                LoadVersion = ProjectFileService.CurrentVersion,
            };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            dialogs.OpenDialogResult = "current-project.ygc";

            await ExecuteAsync(main.ProjectWorkflow.OpenProjectCommand);
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            Assert.AreEqual(0, dialogs.InfoMessageCount);
        }

        /// <summary>
        /// Слепок снимается на потоке интерфейса, а запись уходит в фон:
        /// окно не замирает на время дискового ввода-вывода.
        /// </summary>
        [TestMethod]
        public async Task SaveProject_WritesOnBackgroundThread()
        {
            var projectFiles = new ThreadRecordingProjectFileService();
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            main.OperationsWorkspace.AllOperations.Add(new DrillPointsOperation());
            dialogs.SaveDialogResult = "threaded-project.ygc";
            var testThread = System.Threading.Thread.CurrentThread.ManagedThreadId;

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            Assert.AreEqual(testThread, projectFiles.SerializeThread, "Слепок — на вызывающем потоке");
            Assert.AreNotEqual(testThread, projectFiles.WriteThread, "Запись — в фоне");
        }

        private sealed class ThreadRecordingProjectFileService : IProjectFileService
        {
            public int SerializeThread { get; private set; }

            public int WriteThread { get; private set; }

            public void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => SaveSerialized(filePath, Serialize(operations, settings));

            public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
            {
                SerializeThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                return string.Empty;
            }

            public void SaveSerialized(string filePath, string json)
                => WriteThread = System.Threading.Thread.CurrentThread.ManagedThreadId;

            public ProjectFileData Load(string filePath)
                => throw new System.NotSupportedException();
        }

        [TestMethod]
        public async Task SaveProject_DelegatesSelectedPathOperationsAndActiveSettings()
        {
            const string filePath = "virtual-project.ygc";
            var projectFiles = new RecordingProjectFileService();
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            var operation = new DrillPointsOperation();
            main.OperationsWorkspace.AllOperations.Add(operation);
            settingsStore.Current.Spindle.SpindleSpeedRpm = 7300;
            dialogs.SaveDialogResult = filePath;

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            Assert.AreEqual(filePath, projectFiles.FilePath);
            Assert.AreSame(main.OperationsWorkspace.AllOperations, projectFiles.Operations);
            Assert.AreSame(settingsStore.Current, projectFiles.Settings);
            Assert.AreSame(operation, projectFiles.Operations[0]);
            Assert.AreEqual(7300, projectFiles.Settings.Spindle.SpindleSpeedRpm);
        }
    }
}
