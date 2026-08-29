#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Threading;
using Autofac;
using Autofac.Features.Indexed;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.Tests.Fixtures;
using GCodeGenerator.ViewModels;
using GCodeGenerator.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public sealed class LiveDialogBindingTests
    {
        [TestMethod]
        public void EveryOperationEditor_LoadsWithRealViewModelWithoutBindingErrors()
        {
            var problems = new List<string>();
            var checkedEditors = 0;

            TestApplication.Run(() =>
            {
                var bindingTrace = PresentationTraceSources.DataBindingSource;
                var previousLevel = bindingTrace.Switch.Level;
                var listener = new RecordingTraceListener();
                bindingTrace.Listeners.Add(listener);
                bindingTrace.Switch.Level = SourceLevels.Error;

                try
                {
                    using var container = ContainerTests.BuildContainer();
                    var editors = container.Resolve<IIndex<Type, IOperationEditorViewModel>>();

                    foreach (var (viewModelType, operation) in EditorCases())
                    {
                        listener.Messages.Clear();
                        Window? window = null;
                        IOperationEditorViewModel? editor = null;
                        try
                        {
                            Assert.IsTrue(editors.TryGetValue(viewModelType, out editor),
                                $"{viewModelType.Name}: view-model не разрешена");
                            editor.SetOperation(operation);
                            window = (Window)Activator.CreateInstance(DialogViewRegistry.ViewFor(viewModelType))!;
                            window.DataContext = editor;
                            window.ShowInTaskbar = false;
                            window.WindowStartupLocation = WindowStartupLocation.Manual;
                            window.Left = -10000;
                            window.Top = -10000;
                            window.Show();
                            window.UpdateLayout();
                            window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                            checkedEditors++;

                            foreach (var message in listener.Messages)
                                problems.Add($"{viewModelType.Name}: {message}");
                        }
                        catch (Exception failure)
                        {
                            problems.Add($"{viewModelType.Name}: {failure.GetBaseException().Message}");
                        }
                        finally
                        {
                            window?.Close();
                            (editor as CloseableViewModel)?.OnClosed();
                        }
                    }
                }
                finally
                {
                    bindingTrace.Listeners.Remove(listener);
                    bindingTrace.Switch.Level = previousLevel;
                    listener.Dispose();
                }
            });

            Assert.AreEqual(19, checkedEditors, "Проверены не все редакторы операций");
            Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
        }

        private static IEnumerable<(Type ViewModelType, OperationBase Operation)> EditorCases()
        {
            foreach (var registration in OperationEditorRegistry.Registrations)
            {
                yield return (
                    registration.Value,
                    (OperationBase)Activator.CreateInstance(registration.Key)!);
            }

            foreach (var registration in OperationEditorRegistry.DrillRegistrations)
            {
                yield return (
                    registration.Value,
                    new DrillPointsOperation { DrillMode = registration.Key });
            }
        }

        private sealed class RecordingTraceListener : TraceListener
        {
            public List<string> Messages { get; } = new List<string>();

            public override void Write(string? message)
            {
            }

            public override void WriteLine(string? message)
            {
                if (!string.IsNullOrWhiteSpace(message))
                    Messages.Add(message.Trim());
            }
        }
    }
}
