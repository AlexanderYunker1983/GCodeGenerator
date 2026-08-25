using System;
using System.Collections.Generic;
using System.Windows.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using GCodeGenerator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Диалог настроек: перенос значений, OK/отмена и предпросмотр темы.
    ///
    /// Окно настроек и постоянное хранилище работают по одной таблице
    /// <see cref="SettingsMapping"/>, поэтому проверяется прежде всего её
    /// полнота: каждая настройка обязана иметь поле в окне, иначе параметр
    /// сохраняется, но недоступен для правки.
    /// </summary>
    [TestClass]
    public class SettingsDialogTests
    {
        [TestMethod]
        public void EverySetting_HasEditorProperty()
        {
            var missing = new List<string>();

            foreach (var (path, _) in SettingsMapping.Entries)
            {
                var property = SettingsViewModel.EditorProperty(path);
                if (property == null || !property.CanRead || !property.CanWrite)
                {
                    missing.Add(path);
                    continue;
                }

                var settingType = SettingsMapping.GetValue(new GCodeSettings(), path)?.GetType();
                if (settingType != null && property.PropertyType != settingType)
                    missing.Add($"{path}: тип окна {property.PropertyType.Name} вместо {settingType.Name}");
            }

            Assert.AreEqual(0, missing.Count, string.Join(Environment.NewLine, missing));
        }

        [TestMethod]
        public void Open_ShowsStoredValues()
        {
            var store = new FakeSettingsStore();
            store.Current.Format.LineNumberStart = 40;
            store.Current.Spindle.SpindleSpeedRpm = 15000;
            store.Current.WorkCoordinate.EndZ = 7.5;
            store.Current.WorkCoordinate.WorkCoordinateSystem = "G56";

            var dialog = new SettingsViewModel(null, store, new FakeThemeService());

            Assert.AreEqual(40, dialog.LineNumberStart);
            Assert.AreEqual(15000, dialog.SpindleSpeedRpm);
            Assert.AreEqual(7.5, dialog.EndZ);
            Assert.AreEqual("G56", dialog.WorkCoordinateSystem);
        }

        [TestMethod]
        public void Ok_SavesEveryChangedSetting()
        {
            var store = new FakeSettingsStore();
            var dialog = new SettingsViewModel(null, store, new FakeThemeService());

            dialog.LineNumberStep = 5;
            dialog.AllowArcs = !dialog.AllowArcs;
            dialog.SpindleDelaySeconds = 2.5;
            dialog.CoolantStartEnabled = !dialog.CoolantStartEnabled;
            dialog.StartX = -12.25;
            dialog.WorkCoordinateSystem = "G55";
            var expectedArcs = dialog.AllowArcs;
            var expectedCoolant = dialog.CoolantStartEnabled;

            Execute(dialog.OkCommand);

            Assert.AreEqual(5, store.Current.Format.LineNumberStep);
            Assert.AreEqual(expectedArcs, store.Current.Format.AllowArcs);
            Assert.AreEqual(2.5, store.Current.Spindle.SpindleDelaySeconds);
            Assert.AreEqual(expectedCoolant, store.Current.Coolant.CoolantStartEnabled);
            Assert.AreEqual(-12.25, store.Current.WorkCoordinate.StartX);
            Assert.AreEqual("G55", store.Current.WorkCoordinate.WorkCoordinateSystem);
            Assert.AreEqual(1, store.SaveCount);
        }

        [TestMethod]
        public void Cancel_KeepsSettingsUntouched()
        {
            var store = new FakeSettingsStore();
            store.Current.Format.LineNumberStep = 1;
            var dialog = new SettingsViewModel(null, store, new FakeThemeService());

            dialog.LineNumberStep = 99;
            Execute(dialog.CancelCommand);
            dialog.OnClosed();

            Assert.AreEqual(1, store.Current.Format.LineNumberStep);
            Assert.AreEqual(0, store.SaveCount);
        }

        [TestMethod]
        public void Ok_ClosesDialog()
        {
            var store = new FakeSettingsStore();
            var dialog = new SettingsViewModel(null, store, new FakeThemeService());
            var closed = 0;
            dialog.CloseRequested += () => closed++;

            Execute(dialog.OkCommand);

            Assert.AreEqual(1, closed);
        }

        [TestMethod]
        public void ThemeSwitch_IsPreviewedImmediately()
        {
            var theme = new FakeThemeService();
            var dialog = new SettingsViewModel(null, new FakeSettingsStore(), theme);

            dialog.UseDarkTheme = true;

            CollectionAssert.AreEqual(new[] { true }, theme.Applied);
        }

        [TestMethod]
        public void Cancel_RestoresPreviewedTheme()
        {
            var theme = new FakeThemeService();
            var store = new FakeSettingsStore();
            var dialog = new SettingsViewModel(null, store, theme);

            dialog.UseDarkTheme = true;
            Execute(dialog.CancelCommand);
            dialog.OnClosed();

            CollectionAssert.AreEqual(new[] { true, false }, theme.Applied);
            Assert.IsFalse(store.Current.Ui.UseDarkTheme);
        }

        [TestMethod]
        public void Ok_KeepsPreviewedTheme()
        {
            var theme = new FakeThemeService();
            var store = new FakeSettingsStore();
            var dialog = new SettingsViewModel(null, store, theme);

            dialog.UseDarkTheme = true;
            Execute(dialog.OkCommand);
            dialog.OnClosed();

            CollectionAssert.AreEqual(new[] { true }, theme.Applied);
            Assert.IsTrue(store.Current.Ui.UseDarkTheme);
        }

        private static void Execute(ICommand command) => command.Execute(null);

        /// <summary>Хранилище настроек в памяти: считает вызовы сохранения.</summary>
        private sealed class FakeSettingsStore : ISettingsStore
        {
            public event EventHandler SettingsChanged;

            public GCodeSettings Current { get; } = new GCodeSettings();

            public int SaveCount { get; private set; }

            public void Save()
            {
                SaveCount++;
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }

            public void RestoreGlobalGenerationSettings()
            {
            }
        }

        /// <summary>Тема без окон: запоминает порядок применённых значений.</summary>
        private sealed class FakeThemeService : IThemeService
        {
            public event EventHandler ThemeChanged;

            public List<bool> Applied { get; } = new List<bool>();

            public void ApplyTheme(bool useDarkTheme)
            {
                Applied.Add(useDarkTheme);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
