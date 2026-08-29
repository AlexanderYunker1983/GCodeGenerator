#nullable enable
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Один пользовательский recovery-файл принадлежит одному процессу.</summary>
    [TestClass]
    public class SingleInstanceCoordinatorTests
    {
        [TestMethod]
        public async Task SecondInstance_ForwardsProjectToLockOwner()
        {
            var directory = TemporaryDirectory();
            var lockPath = Path.Combine(directory, "instance.lock");
            var pipeName = "GCodeGenerator.Tests." + Guid.NewGuid().ToString("N");
            var received = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var expected = Path.Combine(directory, "деталь с пробелом.ygc");

            try
            {
                using var primary = new SingleInstanceCoordinator(
                    lockPath, pipeName, request => received.TrySetResult(request));
                using var secondary = new SingleInstanceCoordinator(
                    lockPath, pipeName, _ => Assert.Fail("Второй экземпляр не должен принимать запросы"));

                Assert.IsTrue(primary.TryAcquire());
                primary.StartListening();
                Assert.IsFalse(secondary.TryAcquire());
                Assert.IsTrue(secondary.TryForward(expected, TimeSpan.FromSeconds(5)));

                var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.AreSame(received.Task, completed, "Первый экземпляр не получил путь");
                Assert.AreEqual(expected, await received.Task);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task EmptyRequest_ActivatesPrimaryAndDisposedOwnerReleasesLock()
        {
            var directory = TemporaryDirectory();
            var lockPath = Path.Combine(directory, "instance.lock");
            var pipeName = "GCodeGenerator.Tests." + Guid.NewGuid().ToString("N");
            var received = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using (var primary = new SingleInstanceCoordinator(
                           lockPath, pipeName, request => received.TrySetResult(request)))
                using (var secondary = new SingleInstanceCoordinator(lockPath, pipeName, _ => { }))
                {
                    Assert.IsTrue(primary.TryAcquire());
                    primary.StartListening();
                    Assert.IsFalse(secondary.TryAcquire());
                    Assert.IsTrue(secondary.TryForward(null, TimeSpan.FromSeconds(5)));
                    var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                    Assert.AreSame(received.Task, completed, "Запрос активации не получен");
                    Assert.IsNull(await received.Task);
                }

                using var replacement = new SingleInstanceCoordinator(lockPath, pipeName, _ => { });
                Assert.IsTrue(replacement.TryAcquire(), "Завершившийся процесс удерживает lock");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public async Task SilentConnectedClient_TimesOutAndDoesNotBlockNextRequest()
        {
            var directory = TemporaryDirectory();
            var lockPath = Path.Combine(directory, "instance.lock");
            var pipeName = "GCodeGenerator.Tests." + Guid.NewGuid().ToString("N");
            var received = new TaskCompletionSource<string?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                using var primary = new SingleInstanceCoordinator(
                    lockPath,
                    pipeName,
                    request => received.TrySetResult(request),
                    requestReadTimeout: TimeSpan.FromMilliseconds(100));
                using var secondary = new SingleInstanceCoordinator(lockPath, pipeName, _ => { });
                Assert.IsTrue(primary.TryAcquire());
                primary.StartListening();

                using (var silent = new NamedPipeClientStream(
                           ".",
                           pipeName,
                           PipeDirection.Out,
                           PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                {
                    silent.Connect(5000);
                    await Task.Delay(250);
                }

                Assert.IsTrue(secondary.TryForward("next-project.ygc", TimeSpan.FromSeconds(5)),
                    "После таймаута молчащего клиента слушатель должен принять следующий запрос");
                var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.AreSame(received.Task, completed, "Следующий запрос не дошёл до основного процесса");
                Assert.AreEqual("next-project.ygc", await received.Task);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static string TemporaryDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "gcodegen-instance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
