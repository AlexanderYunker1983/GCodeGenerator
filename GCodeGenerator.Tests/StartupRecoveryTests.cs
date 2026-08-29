#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Выбор стартового документа после аварийного завершения. Путь проекта
    /// может прийти из Explorer, но он не имеет права молча уничтожить более
    /// свежий autosave до вопроса пользователю.
    /// </summary>
    [TestClass]
    public sealed class StartupRecoveryTests
    {
        [TestMethod]
        public async Task CommandLineProject_WithRecovery_OpensRecoveryFirstAndPreservesIt()
        {
            var recovery = new RecoveryStub(exists: true);
            var messages = new FakeDialogs { ConfirmationResult = true };
            var opened = new List<string>();

            await App.OpenStartupDocumentCoreAsync(
                "old-project.ygc",
                recovery,
                messages,
                path => Record(opened, "project:" + path),
                path => Record(opened, "recovery:" + path),
                "Recover? {0}",
                "Recovery");

            CollectionAssert.AreEqual(
                new[] { "recovery:" + recovery.RecoveryPath },
                opened,
                "выбор восстановления не продолжается открытием старого проекта");
            Assert.AreEqual(0, recovery.ClearCount, "единственный снимок сохранён");
            StringAssert.Contains(messages.LastConfirmationMessage, recovery.RecoveryPath);
        }

        [TestMethod]
        public async Task CommandLineProject_WhenRecoveryDeclined_ClearsSnapshotThenOpensProject()
        {
            var recovery = new RecoveryStub(exists: true);
            var messages = new FakeDialogs { ConfirmationResult = false };
            var opened = new List<string>();

            await App.OpenStartupDocumentCoreAsync(
                "chosen-project.ygc",
                recovery,
                messages,
                path => Record(opened, "project:" + path),
                path => Record(opened, "recovery:" + path),
                "Recover? {0}",
                "Recovery");

            Assert.AreEqual(1, recovery.ClearCount, "отказ пользователя удаляет autosave осознанно");
            CollectionAssert.AreEqual(new[] { "project:chosen-project.ygc" }, opened);
        }

        [TestMethod]
        public async Task CommandLineProject_WithoutRecovery_OpensNormally()
        {
            var recovery = new RecoveryStub(exists: false);
            var messages = new FakeDialogs();
            var opened = new List<string>();

            await App.OpenStartupDocumentCoreAsync(
                "project.ygc",
                recovery,
                messages,
                path => Record(opened, "project:" + path),
                path => Record(opened, "recovery:" + path),
                "Recover? {0}",
                "Recovery");

            CollectionAssert.AreEqual(new[] { "project:project.ygc" }, opened);
            Assert.IsNull(messages.LastConfirmationMessage, "без снимка вопрос не показывается");
        }

        [TestMethod]
        public async Task CorruptPrimary_IsQuarantinedAndBackupIsTriedOnce()
        {
            var recovery = new RecoveryStub(exists: true, backupExists: true);
            var messages = new FakeDialogs { ConfirmationResult = true };
            var opened = new List<string>();

            await App.OpenStartupDocumentCoreAsync(
                null,
                recovery,
                messages,
                path => Record(opened, "project:" + path),
                path => Record(opened, "recovery:" + path, result: path == recovery.BackupPath),
                "Recover? {0}",
                "Recovery");

            CollectionAssert.AreEqual(
                new[]
                {
                    "recovery:" + recovery.RecoveryPath,
                    "recovery:" + recovery.BackupPath,
                },
                opened);
            Assert.AreEqual(1, recovery.QuarantineCount,
                "Повреждённый основной снимок убран из стартового пути");
            Assert.AreEqual(0, recovery.ClearCount, "Резервная копия не удаляется");
        }

        private static Task<bool> Record(ICollection<string> opened, string value, bool result = true)
        {
            opened.Add(value);
            return Task.FromResult(result);
        }

        private sealed class RecoveryStub : IDocumentRecoveryService
        {
            internal RecoveryStub(bool exists, bool backupExists = false)
            {
                Exists = exists;
                BackupExists = backupExists;
            }

            public string RecoveryPath { get; } = "autosave.ygc";
            public string BackupPath { get; } = "autosave.ygc.bak";
            public bool Exists { get; }
            public bool BackupExists { get; }
            public int ClearCount { get; private set; }
            public int QuarantineCount { get; private set; }

            public string? QuarantineCorruptSnapshot()
            {
                QuarantineCount++;
                return RecoveryPath + ".corrupt";
            }

            public void Schedule(Func<string> snapshotFactory)
            {
            }

            public void Clear() => ClearCount++;

            public Task WaitForPendingSaveAsync() => Task.CompletedTask;
        }
    }
}
