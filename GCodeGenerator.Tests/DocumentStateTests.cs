using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using GCodeGenerator.Models;
using GCodeGenerator.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Состояние документа: файл проекта и несохранённые изменения.
    ///
    /// Без них программа не отличала сохранённый проект от изменённого —
    /// закрытие окна молча теряло работу, а «Сохранить проект» каждый раз
    /// спрашивало имя файла заново.
    /// </summary>
    [TestClass]
    public class DocumentStateTests
    {
        private string _projectPath;

        [TestInitialize]
        public void CreateTempPath()
        {
            _projectPath = Path.Combine(Path.GetTempPath(), $"gcodegen_doc_{Guid.NewGuid():N}.ygc");
        }

        [TestCleanup]
        public void RemoveTempFile()
        {
            if (File.Exists(_projectPath))
                File.Delete(_projectPath);
        }

        private static Task ExecuteAsync(System.Windows.Input.ICommand command)
            => ((IAsyncRelayCommand)command).ExecuteAsync(null);

        private static DrillPointsOperation Drill()
            => new DrillPointsOperation
            {
                Holes = { new DrillHole { X = 1, Y = 2, TotalDepth = 2, StepDepth = 1 } }
            };

        [TestMethod]
        public void NewDocument_IsClean()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();

            Assert.IsFalse(main.ConfirmClose() == false, "Пустой документ закрывается без вопросов");
            Assert.AreEqual(0, dialogs.SaveConfirmationCount, "Вопроса быть не должно");
        }

        [TestMethod]
        public void AddingOperation_MakesDocumentDirty()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());

            main.ConfirmClose();

            Assert.AreEqual(1, dialogs.SaveConfirmationCount,
                "Изменённый документ спрашивает о сохранении при закрытии");
        }

        [TestMethod]
        public void ChangingGenerationSettings_MakesDocumentDirty()
        {
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain();

            // Настройки генерации сохраняются вместе с проектом.
            settingsStore.Current.Format.UseComments = !settingsStore.Current.Format.UseComments;
            settingsStore.Save();
            main.ConfirmClose();

            Assert.AreEqual(1, dialogs.SaveConfirmationCount,
                "Правка настроек генерации делает проект несохранённым");
        }

        /// <summary>
        /// Смена темы или языка — дело приложения, а не документа: прежде
        /// любой OK окна настроек помечал проект несохранённым, даже если
        /// в нём меняли только тему.
        /// </summary>
        [TestMethod]
        public void UiOnlySettingsSave_KeepsDocumentClean()
        {
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain();

            settingsStore.Current.Ui.UseDarkTheme = !settingsStore.Current.Ui.UseDarkTheme;
            settingsStore.Save();
            main.ConfirmClose();

            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Смена темы не делает проект несохранённым");
        }

        [TestMethod]
        public async Task Saving_ClearsDirtyFlagAndRemembersFile()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            Assert.IsTrue(File.Exists(_projectPath), "Проект записан в файл");
            main.ConfirmClose();
            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Сохранённый документ закрывается без вопросов");
        }

        /// <summary>
        /// Повторное сохранение пишет в тот же файл: имя спрашивается только
        /// у проекта, которого ещё нет на диске.
        /// </summary>
        [TestMethod]
        public async Task SavingTwice_DoesNotAskForFileNameAgain()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            dialogs.SaveDialogResult = null; // диалог выбора файла ответил бы отказом
            main.OperationsWorkspace.AllOperations.Add(Drill());
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            main.ConfirmClose();
            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Второе сохранение прошло в тот же файл, изменений не осталось");
        }

        [TestMethod]
        public async Task SaveAs_AlwaysAsksForFileName()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            var otherPath = Path.Combine(Path.GetTempPath(), $"gcodegen_doc_{Guid.NewGuid():N}.ygc");
            dialogs.SaveDialogResult = otherPath;
            try
            {
                await ExecuteAsync(main.ProjectWorkflow.SaveProjectAsCommand);
                Assert.IsTrue(File.Exists(otherPath), "«Сохранить как» пишет в выбранный файл");
            }
            finally
            {
                if (File.Exists(otherPath))
                    File.Delete(otherPath);
            }
        }

        [TestMethod]
        public async Task OpeningProject_StartsClean()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);
            await ExecuteAsync(main.ProjectWorkflow.NewProgramCommand);

            dialogs.OpenDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.OpenProjectCommand);

            Assert.AreEqual(1, main.OperationsWorkspace.AllOperations.Count, "Проект открыт");
            main.ConfirmClose();
            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Только что открытый проект несохранённым не считается");
        }

        /// <summary>
        /// Ответ «Отмена» на вопрос о несохранённых изменениях останавливает
        /// и закрытие программы, и создание нового проекта.
        /// </summary>
        [TestMethod]
        public async Task CancelAnswer_StopsTheAction()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Cancel;

            Assert.IsFalse(main.ConfirmClose(), "Закрытие отменено");

            await ExecuteAsync(main.ProjectWorkflow.NewProgramCommand);
            Assert.AreEqual(1, main.OperationsWorkspace.AllOperations.Count, "Создание нового проекта отменено");
        }

        [TestMethod]
        public void SaveAnswer_WritesFileAndAllowsTheAction()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Save;
            dialogs.SaveDialogResult = _projectPath;

            var allowed = main.ConfirmClose();

            Assert.IsTrue(allowed, "После сохранения закрытие разрешено");
            Assert.IsTrue(File.Exists(_projectPath), "Проект сохранён перед закрытием");
        }

        /// <summary>
        /// Ответ «Сохранить» с последующей отменой выбора файла не должен
        /// приводить к потере работы: действие отменяется целиком.
        /// </summary>
        [TestMethod]
        public void SaveAnswer_WithCancelledFileDialog_StopsTheAction()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Save;
            dialogs.SaveDialogResult = null;

            Assert.IsFalse(main.ConfirmClose(), "Несохранённый проект не закрывается");
        }

        [TestMethod]
        public void DiscardAnswer_AllowsTheActionWithoutSaving()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Discard;

            Assert.IsTrue(main.ConfirmClose(), "Пользователь согласился потерять изменения");
            Assert.IsFalse(File.Exists(_projectPath), "Ничего не сохранялось");
        }

        /// <summary>
        /// Заголовок окна показывает, с каким файлом идёт работа и есть ли
        /// несохранённые изменения.
        /// </summary>
        [TestMethod]
        public async Task WindowTitle_ShowsFileNameAndChangeMark()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            StringAssert.Contains(main.DisplayName, "UntitledProject",
                "У несохранённого проекта вместо имени файла — «без имени»");

            main.OperationsWorkspace.AllOperations.Add(Drill());
            StringAssert.Contains(main.DisplayName, "*", "Изменения отмечены звёздочкой");

            dialogs.SaveDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            StringAssert.Contains(main.DisplayName, Path.GetFileName(_projectPath),
                "После сохранения виден файл проекта");
            Assert.IsFalse(main.DisplayName.Contains("*"), "Звёздочка снята");
        }

        /// <summary>
        /// Сбой записи не выдаёт проект за сохранённый: признак изменений
        /// сбрасывается только когда данные действительно на диске.
        /// </summary>
        [TestMethod]
        public async Task FailedSave_KeepsDocumentDirty()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.OperationsWorkspace.AllOperations.Add(Drill());
            // Недопустимое для файловой системы имя: запись падает.
            dialogs.SaveDialogResult = "?:\\<>|\\project.ygc";

            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);

            Assert.IsFalse(string.IsNullOrEmpty(dialogs.LastErrorMessage), "Сбой показан");
            StringAssert.Contains(main.DisplayName, "*", "Изменения по-прежнему несохранённые");
        }

        /// <summary>
        /// Сбой на полпути замены документа — например, в обработчике нового
        /// содержимого — возвращает прежние операции: документ либо заменён
        /// целиком, либо остался прежним, а признак изменений не сбрасывается.
        /// </summary>
        [TestMethod]
        public async Task FailedDocumentApply_RestoresPreviousOperations()
        {
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain();
            var existing = Drill();
            main.OperationsWorkspace.AllOperations.Add(existing);
            dialogs.SaveDialogResult = _projectPath;
            await ExecuteAsync(main.ProjectWorkflow.SaveProjectCommand);
            main.OperationsWorkspace.AllOperations.Add(Drill());
            settingsStore.Current.Spindle.SpindleSpeedRpm = 4321;
            settingsStore.Save();
            dialogs.SaveConfirmationResult = SaveConfirmation.Discard;
            dialogs.OpenDialogResult = _projectPath;
            // Падение только на операциях из файла: прежние операции откат
            // обязан вернуть беспрепятственно — как обработчик, которому
            // плохо от конкретного нового содержимого.
            var known = new System.Collections.Generic.HashSet<OperationBase>(
                main.OperationsWorkspace.AllOperations);
            var failing = new System.Collections.Specialized.NotifyCollectionChangedEventHandler(
                (_, e) =>
                {
                    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                        && e.NewItems?[0] is OperationBase added
                        && !known.Contains(added))
                    {
                        throw new InvalidOperationException("preview failure");
                    }
                });
            main.OperationsWorkspace.AllOperations.CollectionChanged += failing;

            try
            {
                await ExecuteAsync(main.ProjectWorkflow.OpenProjectCommand);
            }
            finally
            {
                main.OperationsWorkspace.AllOperations.CollectionChanged -= failing;
            }

            Assert.IsFalse(string.IsNullOrEmpty(dialogs.LastErrorMessage), "Сбой открытия показан");
            Assert.AreEqual(2, main.OperationsWorkspace.AllOperations.Count, "Прежний документ возвращён");
            Assert.AreSame(existing, main.OperationsWorkspace.AllOperations[0], "Те же операции, не копии");
            Assert.AreEqual(4321, settingsStore.Current.Spindle.SpindleSpeedRpm,
                "Настройки откатываются вместе с операциями");
            StringAssert.Contains(main.DisplayName, "*",
                "Несохранённые изменения не выданы за сохранённые");
        }
    }
}
