using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class GCodeFileServiceTests
    {
        private sealed class RecordingGCodeFileService : IGCodeFileService
        {
            public string FilePath { get; private set; }
            public string GCode { get; private set; }

            public void Save(string filePath, string gCode)
            {
                FilePath = filePath;
                GCode = gCode;
            }
        }

        [TestMethod]
        public void MainViewModel_SaveGCode_DelegatesToFileService()
        {
            const string filePath = "virtual-program.nc";
            const string gCode = "G0 X1 Y2\r\nM30\r\n";
            var fileService = new RecordingGCodeFileService();
            var (main, _, dialog, _) = MainViewModelOperationEditTests.CreateMain(
                gCodeFileService: fileService);
            main.GCodeWorkflow.GCodePreview = gCode;
            dialog.SaveDialogResult = filePath;

            main.GCodeWorkflow.SaveGCodeCommand.Execute(null);

            Assert.AreEqual(filePath, fileService.FilePath);
            Assert.AreEqual(gCode, fileService.GCode);
        }
    }
}
