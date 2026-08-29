#nullable enable
using System;
using System.Configuration;
using System.IO;
using GCodeGenerator.Diagnostics;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Повреждённый user.config не должен превращать запуск в бесконечный
    /// цикл фатальных ошибок. Файл сохраняется для диагностики, а новый
    /// экземпляр настроек начинает с объявленных значений по умолчанию.
    /// </summary>
    [TestClass]
    public sealed class CorruptSettingsRecoveryTests
    {
        [TestMethod]
        public void BrokenUserConfig_IsQuarantinedAndSecondLoadContinues()
        {
            var directory = TemporaryDirectory();
            var userConfig = Path.Combine(directory, "user.config");
            File.WriteAllText(userConfig, "<broken");
            var logger = new RecordingLogger();
            var factoryCalls = 0;
            var probeCalls = 0;

            try
            {
                _ = new ApplicationPersistedSettings(
                    logger,
                    () =>
                    {
                        factoryCalls++;
                        return new TestApplicationSettings();
                    },
                    _ =>
                    {
                        probeCalls++;
                        if (probeCalls == 1)
                            throw new ConfigurationErrorsException("broken XML", userConfig, 1);
                    },
                    () => new DateTime(2026, 8, 29, 12, 34, 56, DateTimeKind.Utc));

                Assert.AreEqual(2, factoryCalls, "после переноса создаётся чистый экземпляр настроек");
                Assert.AreEqual(2, probeCalls, "значения по умолчанию проверяются повторным чтением");
                Assert.IsFalse(File.Exists(userConfig), "битый файл больше не блокирует следующий запуск");

                var quarantined = userConfig + ".corrupt-20260829T123456000Z";
                Assert.IsTrue(File.Exists(quarantined), "исходный файл сохранён для диагностики");
                Assert.AreEqual("<broken", File.ReadAllText(quarantined));
                Assert.AreEqual(1, logger.WarningCount);
                StringAssert.Contains(logger.LastMessage, userConfig);
                StringAssert.Contains(logger.LastMessage, quarantined);
                Assert.IsInstanceOfType<ConfigurationErrorsException>(logger.LastException);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void BrokenApplicationConfig_IsNotRenamedAsUserData()
        {
            var directory = TemporaryDirectory();
            var applicationConfig = Path.Combine(directory, "GCodeGenerator.dll.config");
            File.WriteAllText(applicationConfig, "<broken");

            try
            {
                Assert.Throws<ConfigurationErrorsException>(() =>
                    new ApplicationPersistedSettings(
                        NullAppLogger.Instance,
                        () => new TestApplicationSettings(),
                        _ => throw new ConfigurationErrorsException("broken XML", applicationConfig, 1),
                        () => DateTime.UtcNow));

                Assert.IsTrue(File.Exists(applicationConfig),
                    "автоматически изолируется только пользовательский user.config");
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        private static string TemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "gcg-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class RecordingLogger : IAppLogger
        {
            internal int WarningCount { get; private set; }
            internal string LastMessage { get; private set; } = string.Empty;
            internal Exception? LastException { get; private set; }

            public void Log(LogLevel level, string message, Exception? exception = null)
            {
                if (level != LogLevel.Warning)
                    return;

                WarningCount++;
                LastMessage = message;
                LastException = exception;
            }
        }

        private sealed class TestApplicationSettings : ApplicationSettingsBase
        {
        }
    }
}
