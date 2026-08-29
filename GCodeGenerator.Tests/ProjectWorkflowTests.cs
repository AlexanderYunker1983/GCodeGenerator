using System.Collections.Generic;
using System.Threading;
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

        private sealed class BlockingProjectFileService : IProjectFileService
        {
            private readonly ManualResetEventSlim _continueWrite = new ManualResetEventSlim(false);

            public TaskCompletionSource<bool> WriteStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public void ContinueWrite() => _continueWrite.Set();

            public void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => SaveSerialized(filePath, Serialize(operations, settings));

            public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => "snapshot";

            public void SaveSerialized(string filePath, string json)
            {
                WriteStarted.TrySetResult(true);
                _continueWrite.Wait();
            }

            public ProjectFileData Load(string filePath)
                => throw new System.NotSupportedException();
        }

        private sealed class SerializedLoadProjectFileService : IProjectFileService
        {
            private readonly ManualResetEventSlim _continueFirstLoad = new ManualResetEventSlim(false);
            private int _activeLoads;
            private int _maximumConcurrentLoads;

            public TaskCompletionSource<bool> FirstLoadStarted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public int MaximumConcurrentLoads => _maximumConcurrentLoads;

            public void ContinueFirstLoad() => _continueFirstLoad.Set();

            public void Save(string filePath, IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => throw new System.NotSupportedException();

            public string Serialize(IReadOnlyList<OperationBase> operations, GCodeSettings settings)
                => throw new System.NotSupportedException();

            public void SaveSerialized(string filePath, string json)
                => throw new System.NotSupportedException();

            public ProjectFileData Load(string filePath)
            {
                var active = Interlocked.Increment(ref _activeLoads);
                int observed;
                do
                {
                    observed = _maximumConcurrentLoads;
                    if (observed >= active)
                        break;
                }
                while (Interlocked.CompareExchange(ref _maximumConcurrentLoads, active, observed) != observed);

                try
                {
                    if (filePath == "first.ygc")
                    {
                        FirstLoadStarted.TrySetResult(true);
                        _continueFirstLoad.Wait();
                    }

                    return new ProjectFileData
                    {
                        Version = ProjectFileService.CurrentVersion,
                        Operations = new List<OperationBase>
                        {
                            new DrillPointsOperation { Name = filePath }
                        },
                    };
                }
                finally
                {
                    Interlocked.Decrement(ref _activeLoads);
                }
            }
        }

        [TestMethod]
        public async Task EditWhileSaveIsWriting_KeepsDocumentDirty()
        {
            var projectFiles = new BlockingProjectFileService();
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            main.OperationsWorkspace.AllOperations.Add(new DrillPointsOperation());
            dialogs.SaveDialogResult = "snapshot.ygc";

            var saving = ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);
            await projectFiles.WriteStarted.Task;
            main.OperationsWorkspace.AllOperations.Add(new DrillPointsOperation());
            projectFiles.ContinueWrite();
            await saving;

            Assert.AreEqual("snapshot.ygc", main.ProjectWorkflow.CurrentPath, "Слепок записан в выбранный файл");
            Assert.IsTrue(main.ProjectWorkflow.IsDirty,
                "Правка, сделанная после слепка, не должна считаться попавшей в файл");
        }

        [TestMethod]
        public async Task ConcurrentOpenRequests_AreAppliedInRequestOrder()
        {
            var projectFiles = new SerializedLoadProjectFileService();
            var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);

            var first = main.OpenProjectAsync("first.ygc");
            await projectFiles.FirstLoadStarted.Task;
            var second = main.OpenProjectAsync("second.ygc");
            await Task.Yield();
            projectFiles.ContinueFirstLoad();

            Assert.IsTrue(await first);
            Assert.IsTrue(await second);
            Assert.AreEqual(1, projectFiles.MaximumConcurrentLoads,
                "Чтение и применение проектов не перекрываются");
            Assert.AreEqual("second.ygc", main.ProjectWorkflow.CurrentPath,
                "Последний запрос остаётся текущим документом");
            Assert.AreEqual("second.ygc", main.OperationsWorkspace.AllOperations[0].Name);
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
