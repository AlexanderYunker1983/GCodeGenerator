using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfImportDelegationTests
    {
        private sealed class RecordingDxfImportService : IDxfImportService
        {
            public string ProfilePath { get; private set; }
            public string PocketPath { get; private set; }
            public List<Polyline2D> ProfileResult { get; } = new List<Polyline2D>
            {
                new Polyline2D
                {
                    Points = new List<Point2D>
                    {
                        new Point2D { X = 0, Y = 0 },
                        new Point2D { X = 1, Y = 0 }
                    }
                }
            };
            public List<Polyline2D> PocketResult { get; } = new List<Polyline2D>
            {
                new Polyline2D
                {
                    Points = new List<Point2D>
                    {
                        new Point2D { X = 0, Y = 0 },
                        new Point2D { X = 1, Y = 0 },
                        new Point2D { X = 0, Y = 0 }
                    }
                }
            };

            public List<Polyline2D> ReadProfilePolylines(string path)
            {
                ProfilePath = path;
                return ProfileResult;
            }

            public List<Polyline2D> ReadPocketClosedContours(string path, CancellationToken cancellation = default)
            {
                PocketPath = path;
                return PocketResult;
            }
        }

        [TestMethod]
        public async Task ProfileViewModel_DelegatesImportToService()
        {
            const string path = "virtual-profile.dxf";
            var dialog = new FakeDialogs { OpenDialogResult = path, OnError = Assert.Fail };
            var importer = new RecordingDxfImportService();
            var vm = new ProfileDxfOperationViewModel(null, dialog, dialog, importer);

            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            Assert.AreEqual(path, importer.ProfilePath);
            Assert.AreSame(importer.ProfileResult, vm.Operation.Polylines);
            Assert.AreEqual(path, vm.Operation.DxfFilePath);
        }

        [TestMethod]
        public async Task PocketViewModel_DelegatesImportToService()
        {
            const string path = "virtual-pocket.dxf";
            var dialog = new FakeDialogs { OpenDialogResult = path, OnError = Assert.Fail };
            var importer = new RecordingDxfImportService();
            var vm = new PocketDxfOperationViewModel(null, dialog, dialog, importer);

            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            Assert.AreEqual(path, importer.PocketPath);
            Assert.AreSame(importer.PocketResult, vm.Operation.ClosedContours);
            Assert.AreEqual(path, vm.Operation.DxfFilePath);
        }
    }
}
