#nullable enable
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>Регистрация безопасного возврата приложения после обновления.</summary>
    [TestClass]
    public class ApplicationRestartTests
    {
        [TestMethod]
        public void SavedProject_IsQuotedForTheRestartCommandLine()
        {
            Assert.AreEqual(
                @"""C:\Jobs\My project.ygc""",
                ApplicationRestartService.CommandLineFor(@"C:\Jobs\My project.ygc"));
            Assert.AreEqual(
                @"""C:\Jobs\folder\\""",
                ApplicationRestartService.CommandLineFor("C:\\Jobs\\folder\\"));
        }

        [TestMethod]
        public void NoProject_RestartsWithAnEmptyDocument()
        {
            Assert.IsNull(ApplicationRestartService.CommandLineFor(null));
            Assert.IsNull(ApplicationRestartService.CommandLineFor("  "));
        }

        [TestMethod]
        public void Registration_AllowsPatchRestartButNotCrashLoop()
        {
            string? commandLine = null;
            ApplicationRestartService.RestartRestrictions restrictions = 0;
            var service = new ApplicationRestartService((line, flags) =>
            {
                commandLine = line;
                restrictions = flags;
                return 0;
            });

            service.Register(@"C:\Jobs\one.ygc");

            Assert.AreEqual(@"""C:\Jobs\one.ygc""", commandLine);
            Assert.IsTrue(restrictions.HasFlag(ApplicationRestartService.RestartRestrictions.NoCrash));
            Assert.IsTrue(restrictions.HasFlag(ApplicationRestartService.RestartRestrictions.NoHang));
            Assert.IsTrue(restrictions.HasFlag(ApplicationRestartService.RestartRestrictions.NoReboot));
            Assert.IsFalse(restrictions.HasFlag(ApplicationRestartService.RestartRestrictions.NoPatch),
                "Именно после обновления приложение должно вернуться");
        }

        [TestMethod]
        public void OversizedProjectPath_FallsBackToRestartWithoutArguments()
        {
            string? commandLine = "not called";
            var service = new ApplicationRestartService((line, _) =>
            {
                commandLine = line;
                return 0;
            });

            service.Register("C:\\" + new string('x', 1100) + ".ygc");

            Assert.IsNull(commandLine, "Слишком длинный аргумент не должен ломать регистрацию целиком");
        }
    }
}
