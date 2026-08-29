using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Автоснимок защищает работу от завершения без exception handler.</summary>
    [TestClass]
    public class DocumentRecoveryTests
    {
        private sealed class InlineContext : SynchronizationContext
        {
            public int PostCount { get; private set; }

            public override void Post(SendOrPostCallback callback, object state)
            {
                PostCount++;
                callback(state);
            }
        }

        [TestMethod]
        public async Task RepeatedEdits_AreDebouncedAndOnlyNewestSnapshotIsWritten()
        {
            var directory = TemporaryDirectory();
            var path = Path.Combine(directory, "autosave.ygc");
            var context = new InlineContext();
            var files = new ProjectFileService();
            var recovery = new DocumentRecoveryService(
                files, null, path, TimeSpan.FromMilliseconds(30), context);
            var captures = 0;
            try
            {
                recovery.Schedule(() =>
                {
                    captures++;
                    return files.Serialize(new[] { Drill("old") }, new GCodeSettings());
                });
                recovery.Schedule(() =>
                {
                    captures++;
                    return files.Serialize(new[] { Drill("new") }, new GCodeSettings());
                });

                await recovery.WaitForPendingSaveAsync();

                Assert.AreEqual(1, captures, "Первая правка отменена до сериализации");
                Assert.IsTrue(context.PostCount > 0, "Снимок запрошен через UI-контекст");
                Assert.AreEqual("new", files.Load(path).Operations[0].Name);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task DirtyDocument_IsAutosavedAndManualSaveClearsRecovery()
        {
            var directory = TemporaryDirectory();
            var recoveryPath = Path.Combine(directory, "autosave.ygc");
            var projectPath = Path.Combine(directory, "manual.ygc");
            var files = new ProjectFileService();
            var recovery = new DocumentRecoveryService(
                files, null, recoveryPath, TimeSpan.Zero, new InlineContext());
            try
            {
                var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain(recovery: recovery);
                dialogs.SaveDialogResult = projectPath;
                main.OperationsWorkspace.AllOperations.Add(Drill("autosaved"));

                await recovery.WaitForPendingSaveAsync();
                Assert.IsTrue(File.Exists(recoveryPath));
                Assert.AreEqual("autosaved", files.Load(recoveryPath).Operations[0].Name);

                await ((IAsyncRelayCommand)main.ProjectWorkflow.SaveProjectCommand).ExecuteAsync(null);

                Assert.IsFalse(File.Exists(recoveryPath), "Подтверждённое сохранение удаляет recovery");
                Assert.IsTrue(File.Exists(projectPath));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task Recovery_OpensAsDirtyUntitledDocument()
        {
            var directory = TemporaryDirectory();
            var path = Path.Combine(directory, "autosave.ygc");
            try
            {
                new ProjectFileService().Save(path, new[] { Drill("recovered") }, new GCodeSettings());
                var (main, _, _, _) = MainViewModelOperationEditTests.CreateMain();

                var opened = await main.ProjectWorkflow.OpenRecoveryAsync(path);

                Assert.IsTrue(opened);
                Assert.AreEqual("recovered", main.OperationsWorkspace.AllOperations[0].Name);
                Assert.IsTrue(main.ProjectWorkflow.IsDirty, "Восстановленное нужно сохранить вручную");
                Assert.IsNull(main.ProjectWorkflow.CurrentPath,
                    "Ctrl+S не должен перезаписать единственный recovery-файл");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task Clear_CancelsPendingWriteAndRemovesBackup()
        {
            var directory = TemporaryDirectory();
            var path = Path.Combine(directory, "autosave.ygc");
            var files = new ProjectFileService();
            var recovery = new DocumentRecoveryService(
                files, null, path, TimeSpan.FromMilliseconds(200), new InlineContext());
            try
            {
                files.Save(path, new[] { Drill("one") }, new GCodeSettings());
                files.Save(path, new[] { Drill("two") }, new GCodeSettings());
                Assert.IsTrue(File.Exists(path + ".bak"));
                recovery.Schedule(() => files.Serialize(new[] { Drill("late") }, new GCodeSettings()));

                recovery.Clear();
                await Task.Delay(250);

                Assert.IsFalse(File.Exists(path));
                Assert.IsFalse(File.Exists(path + ".bak"));
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task CompletedSave_CanBeScheduledAgain()
        {
            var directory = TemporaryDirectory();
            var path = Path.Combine(directory, "autosave.ygc");
            var files = new ProjectFileService();
            var recovery = new DocumentRecoveryService(
                files, null, path, TimeSpan.Zero, new InlineContext());
            try
            {
                recovery.Schedule(() => files.Serialize(new[] { Drill("first") }, new GCodeSettings()));
                await recovery.WaitForPendingSaveAsync();

                recovery.Schedule(() => files.Serialize(new[] { Drill("second") }, new GCodeSettings()));
                await recovery.WaitForPendingSaveAsync();

                Assert.AreEqual("second", files.Load(path).Operations[0].Name);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static DrillPointsOperation Drill(string name)
            => new DrillPointsOperation
            {
                Name = name,
                Holes = { new DrillHole { X = 1, Y = 2, TotalDepth = 2, StepDepth = 1 } }
            };

        private static string TemporaryDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "gcodegen-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
