using System.Collections.Generic;
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
            {
                FilePath = filePath;
                Operations = operations;
                Settings = settings;
            }

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
                => SaveCount++;

            public ProjectFileData Load(string filePath)
                => new ProjectFileData
                {
                    Version = LoadVersion,
                    Operations = new List<OperationBase> { new DrillPointsOperation() },
                };
        }

        /// <summary>
        /// Файл старого формата после сохранения молча становился файлом
        /// текущей версии — прежние сборки его больше не откроют, и раньше
        /// пользователь узнавал об этом только там, где файл уже не открылся.
        /// Теперь сохранение сообщает об апгрейде, и ровно один раз: после
        /// него файл уже текущей версии.
        /// </summary>
        [TestMethod]
        public void SavingProjectLoadedFromOlderFormat_WarnsAboutUpgradeOnce()
        {
            var projectFiles = new VersionedProjectFileService { LoadVersion = 2 };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            dialogs.OpenDialogResult = "old-project.ygc";

            main.ProjectWorkflow.OpenProjectCommand.Execute(null);
            Assert.AreEqual(0, dialogs.InfoMessageCount, "открытие само по себе ничего не сообщает");

            main.ProjectWorkflow.SaveProjectCommand.Execute(null);
            Assert.AreEqual(1, dialogs.InfoMessageCount, "первое сохранение сообщает об апгрейде формата");
            Assert.AreEqual("ProjectUpgradedFromOlderVersionInfo", dialogs.LastInfoMessage,
                "текст сообщения берётся из словаря локализации");

            main.ProjectWorkflow.SaveProjectCommand.Execute(null);
            Assert.AreEqual(1, dialogs.InfoMessageCount, "файл уже текущей версии — повторного сообщения нет");
            Assert.AreEqual(2, projectFiles.SaveCount, "оба сохранения при этом состоялись");
        }

        /// <summary>Файл текущей версии сохраняется без сообщений.</summary>
        [TestMethod]
        public void SavingProjectLoadedFromCurrentFormat_StaysSilent()
        {
            var projectFiles = new VersionedProjectFileService
            {
                LoadVersion = ProjectFileService.CurrentVersion,
            };
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            dialogs.OpenDialogResult = "current-project.ygc";

            main.ProjectWorkflow.OpenProjectCommand.Execute(null);
            main.ProjectWorkflow.SaveProjectCommand.Execute(null);

            Assert.AreEqual(0, dialogs.InfoMessageCount);
        }

        [TestMethod]
        public void SaveProject_DelegatesSelectedPathOperationsAndActiveSettings()
        {
            const string filePath = "virtual-project.ygc";
            var projectFiles = new RecordingProjectFileService();
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            var operation = new DrillPointsOperation();
            main.OperationsWorkspace.AllOperations.Add(operation);
            settingsStore.Current.Spindle.SpindleSpeedRpm = 7300;
            dialogs.SaveDialogResult = filePath;

            main.ProjectWorkflow.SaveProjectCommand.Execute(null);

            Assert.AreEqual(filePath, projectFiles.FilePath);
            Assert.AreSame(main.OperationsWorkspace.AllOperations, projectFiles.Operations);
            Assert.AreSame(settingsStore.Current, projectFiles.Settings);
            Assert.AreSame(operation, projectFiles.Operations[0]);
            Assert.AreEqual(7300, projectFiles.Settings.Spindle.SpindleSpeedRpm);
        }
    }
}
