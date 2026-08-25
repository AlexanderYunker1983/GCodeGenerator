using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using GCodeGenerator.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Поведение программы после необработанного сбоя.
    ///
    /// Прежде гасилось всё подряд: программа продолжала работу после любого
    /// исключения, и пользователь сохранял поверх проекта то, что осталось
    /// от модели. Здесь проверяется обратное: продолжается работа только
    /// после отказов, которые документа не касаются, а в остальных случаях
    /// проект уходит в отдельный файл.
    /// </summary>
    [TestClass]
    public class CrashHandlerTests
    {
        [TestMethod]
        public void ExternalResourceFailures_AllowWorkToContinue()
        {
            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(new IOException("файл занят")));
            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(new UnauthorizedAccessException()));
            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(new OperationCanceledException()));
            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(new ExternalException("буфер обмена занят")));
        }

        /// <summary>
        /// Сбой в самой модели или в разметке оставляет состояние неизвестным:
        /// продолжать нельзя, потому что дальше пользователь сохранит
        /// неизвестно что.
        /// </summary>
        [TestMethod]
        public void UnknownFailures_StopTheApplication()
        {
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(new NullReferenceException()));
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(new InvalidOperationException()));
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(new IndexOutOfRangeException()));
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(null));
        }

        /// <summary>
        /// Исключение интерфейса приходит обёрнутым: причина лежит внутри,
        /// и решение принимается по ней, а не по обёртке.
        /// </summary>
        [TestMethod]
        public void WrappedFailures_AreClassifiedByTheirCause()
        {
            var wrappedIo = new InvalidOperationException("не удалось прочитать", new IOException());
            var wrappedNull = new InvalidOperationException("сбой", new NullReferenceException());

            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(wrappedIo));
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(wrappedNull));
        }

        /// <summary>
        /// У составного исключения задачи причин несколько: одной опасной
        /// достаточно, чтобы остановиться.
        /// </summary>
        [TestMethod]
        public void AggregateFailure_StopsIfAnyCauseIsUnknown()
        {
            var harmless = new AggregateException(new IOException(), new OperationCanceledException());
            var dangerous = new AggregateException(new IOException(), new NullReferenceException());

            Assert.AreEqual(CrashResponse.Continue, CrashHandler.Classify(harmless));
            Assert.AreEqual(CrashResponse.Shutdown, CrashHandler.Classify(dangerous));
        }

        /// <summary>
        /// Снимок пишется отдельным файлом с меткой времени и читается
        /// обратно как обычный проект: иначе он не спасает работу, а только
        /// создаёт видимость спасения.
        /// </summary>
        [TestMethod]
        public void Snapshot_IsWrittenAndReadsBackAsProject()
        {
            var directory = Path.Combine(Path.GetTempPath(), "gcodegen-crash-" + Guid.NewGuid().ToString("N"));
            try
            {
                var projectFiles = new ProjectFileService();
                var handler = new CrashHandler(projectFiles, null, directory);
                var operations = new List<OperationBase>
                {
                    new DrillPointsOperation
                    {
                        Name = "Сверление",
                        Holes = { new DrillHole { X = 3, Y = 4, TotalDepth = 2, StepDepth = 1 } },
                    },
                };

                var path = handler.TrySaveSnapshot(operations, new GCodeSettings(), new DateTime(2026, 8, 25, 17, 5, 9));

                Assert.IsNotNull(path, "Снимок должен быть сохранён");
                StringAssert.EndsWith(path, "crash-20260825-170509.ygc");
                Assert.IsTrue(File.Exists(path));

                var restored = projectFiles.Load(path);

                Assert.AreEqual(1, restored.Operations.Count);
                var drill = (DrillPointsOperation)restored.Operations[0];
                Assert.AreEqual("Сверление", drill.Name);
                Assert.AreEqual(3, drill.Holes[0].X);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// Пустой документ сохранять незачем: файл-пустышка среди снимков
        /// сбивает с толку сильнее, чем его отсутствие.
        /// </summary>
        [TestMethod]
        public void Snapshot_OfEmptyDocument_IsNotWritten()
        {
            var directory = Path.Combine(Path.GetTempPath(), "gcodegen-crash-" + Guid.NewGuid().ToString("N"));
            var handler = new CrashHandler(new ProjectFileService(), null, directory);

            var path = handler.TrySaveSnapshot(new List<OperationBase>(), new GCodeSettings(), DateTime.Now);

            Assert.IsNull(path);
            Assert.IsFalse(Directory.Exists(directory), "Каталог снимков не создаётся впустую");
        }

        /// <summary>
        /// Аварийное сохранение выполняется уже после сбоя, поэтому само
        /// упасть не должно: недоступный путь превращается в запись в журнал
        /// и отсутствие снимка, а не во второе исключение поверх первого.
        /// </summary>
        [TestMethod]
        public void Snapshot_ToUnavailablePath_FailsQuietly()
        {
            var handler = new CrashHandler(new ProjectFileService(), null, "?:\\<>|\\crash");
            var operations = new List<OperationBase> { new DrillPointsOperation() };

            var path = handler.TrySaveSnapshot(operations, new GCodeSettings(), DateTime.Now);

            Assert.IsNull(path);
        }
    }
}
