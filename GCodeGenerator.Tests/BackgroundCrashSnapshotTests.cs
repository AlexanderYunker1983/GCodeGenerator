#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Tests.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public sealed class BackgroundCrashSnapshotTests
    {
        [TestMethod]
        public async Task BackgroundCrash_InvokesSnapshotOnUiThread()
        {
            Dispatcher? dispatcher = null;
            TestApplication.Run(() => dispatcher = Dispatcher.CurrentDispatcher);
            Assert.IsNotNull(dispatcher);
            var callbackThread = 0;

            var path = await Task.Run(() => App.TryInvokeCrashSnapshot(
                dispatcher,
                () =>
                {
                    callbackThread = Environment.CurrentManagedThreadId;
                    return "crash-snapshot.ygc";
                },
                new RecordingLogger(),
                TimeSpan.FromSeconds(2)));

            Assert.AreEqual("crash-snapshot.ygc", path);
            Assert.AreEqual(dispatcher.Thread.ManagedThreadId, callbackThread,
                "Документ читается только с владеющего им потока интерфейса");
        }

        [TestMethod]
        public void BusyUiThread_DoesNotBlockCrashHandlerPastTimeout()
        {
            Dispatcher? dispatcher = null;
            TestApplication.Run(() => dispatcher = Dispatcher.CurrentDispatcher);
            Assert.IsNotNull(dispatcher);
            using var blockingStarted = new ManualResetEventSlim();
            using var releaseDispatcher = new ManualResetEventSlim();
            var logger = new RecordingLogger();

            dispatcher.BeginInvoke(() =>
            {
                blockingStarted.Set();
                releaseDispatcher.Wait(TimeSpan.FromSeconds(2));
            }, DispatcherPriority.Send);
            Assert.IsTrue(blockingStarted.Wait(TimeSpan.FromSeconds(2)));

            try
            {
                var path = App.TryInvokeCrashSnapshot(
                    dispatcher,
                    () => "must-not-run.ygc",
                    logger,
                    TimeSpan.FromMilliseconds(50));

                Assert.IsNull(path);
                Assert.AreEqual(1, logger.Errors.Count,
                    "Таймаут сохраняется в журнале, но не заменяет исходный сбой");
                Assert.IsInstanceOfType<TimeoutException>(logger.Errors[0]);
            }
            finally
            {
                releaseDispatcher.Set();
            }
        }

        private sealed class RecordingLogger : IAppLogger
        {
            public List<Exception?> Errors { get; } = new List<Exception?>();

            public void Log(LogLevel level, string message, Exception? exception = null)
            {
                if (level == LogLevel.Error)
                    Errors.Add(exception);
            }
        }
    }
}
