using System.Collections.Generic;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public void SaveProject_DelegatesSelectedPathOperationsAndActiveSettings()
        {
            const string filePath = "virtual-project.ygc";
            var projectFiles = new RecordingProjectFileService();
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain(
                projectFileService: projectFiles);
            var operation = new DrillPointsOperation();
            main.AllOperations.Add(operation);
            settingsStore.Current.Spindle.SpindleSpeedRpm = 7300;
            dialogs.SaveDialogResult = filePath;

            main.SaveProjectCommand.Execute(null);

            Assert.AreEqual(filePath, projectFiles.FilePath);
            Assert.AreSame(main.AllOperations, projectFiles.Operations);
            Assert.AreSame(settingsStore.Current, projectFiles.Settings);
            Assert.AreSame(operation, projectFiles.Operations[0]);
            Assert.AreEqual(7300, projectFiles.Settings.Spindle.SpindleSpeedRpm);
        }
    }
}
