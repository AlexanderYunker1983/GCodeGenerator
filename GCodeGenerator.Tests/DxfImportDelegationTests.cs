using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Import;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels.Pocket;
using GCodeGenerator.ViewModels.PocketMill;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    public class DxfImportDelegationTests
    {
        private sealed class StubDialogService : IDialogService
        {
            public string FilePath { get; set; }

            public void ShowInfo(string message, string title = "") { }
            public void ShowError(string message, string title = "") => Assert.Fail(message);
            public bool ShowConfirm(string message, string title = "") => true;
            public SaveConfirmation ShowSaveConfirmation(string message, string title = "") => SaveConfirmation.Discard;
            public string ShowOpenDialog(string title, string filter, string defaultExtension = "") => FilePath;
            public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "") => null;
            public TViewModel CreateViewModel<TViewModel>() where TViewModel : class => throw new NotSupportedException();
            public object CreateViewModel(Type viewModelType) => throw new NotSupportedException();
            public void ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class => throw new NotSupportedException();
            public void ShowDialog(Type viewModelType, object viewModel) => throw new NotSupportedException();
        }

        private sealed class RecordingDxfImportService : IDxfImportService
        {
            public string ProfilePath { get; private set; }
            public string PocketPath { get; private set; }
            public List<DxfPolyline> ProfileResult { get; } = new List<DxfPolyline>
            {
                new DxfPolyline
                {
                    Points = new List<DxfPoint>
                    {
                        new DxfPoint { X = 0, Y = 0 },
                        new DxfPoint { X = 1, Y = 0 }
                    }
                }
            };
            public List<DxfPolyline> PocketResult { get; } = new List<DxfPolyline>
            {
                new DxfPolyline
                {
                    Points = new List<DxfPoint>
                    {
                        new DxfPoint { X = 0, Y = 0 },
                        new DxfPoint { X = 1, Y = 0 },
                        new DxfPoint { X = 0, Y = 0 }
                    }
                }
            };

            public List<DxfPolyline> ReadProfilePolylines(string path)
            {
                ProfilePath = path;
                return ProfileResult;
            }

            public List<DxfPolyline> ReadPocketClosedContours(string path)
            {
                PocketPath = path;
                return PocketResult;
            }
        }

        [TestMethod]
        public async Task ProfileViewModel_DelegatesImportToService()
        {
            const string path = "virtual-profile.dxf";
            var dialog = new StubDialogService { FilePath = path };
            var importer = new RecordingDxfImportService();
            var vm = new ProfileDxfOperationViewModel(null, dialog, importer);

            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            Assert.AreEqual(path, importer.ProfilePath);
            Assert.AreSame(importer.ProfileResult, vm.Operation.Polylines);
            Assert.AreEqual(path, vm.Operation.DxfFilePath);
        }

        [TestMethod]
        public async Task PocketViewModel_DelegatesImportToService()
        {
            const string path = "virtual-pocket.dxf";
            var dialog = new StubDialogService { FilePath = path };
            var importer = new RecordingDxfImportService();
            var vm = new PocketDxfOperationViewModel(null, dialog, importer);

            await ((IAsyncRelayCommand)vm.ImportDxfCommand).ExecuteAsync(null);

            Assert.AreEqual(path, importer.PocketPath);
            Assert.AreSame(importer.PocketResult, vm.Operation.ClosedContours);
            Assert.AreEqual(path, vm.Operation.DxfFilePath);
        }
    }
}
