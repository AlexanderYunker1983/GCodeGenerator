using System;
using System.IO;
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
            main.AllOperations.Add(Drill());

            main.ConfirmClose();

            Assert.AreEqual(1, dialogs.SaveConfirmationCount,
                "Изменённый документ спрашивает о сохранении при закрытии");
        }

        [TestMethod]
        public void ChangingSettings_MakesDocumentDirty()
        {
            var (main, _, dialogs, settingsStore) = MainViewModelOperationEditTests.CreateMain();

            // Настройки генерации сохраняются вместе с проектом.
            settingsStore.Save();
            main.ConfirmClose();

            Assert.AreEqual(1, dialogs.SaveConfirmationCount,
                "Правка настроек делает проект несохранённым");
        }

        [TestMethod]
        public void Saving_ClearsDirtyFlagAndRemembersFile()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;

            main.SaveProjectCommand.Execute(null);

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
        public void SavingTwice_DoesNotAskForFileNameAgain()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            main.SaveProjectCommand.Execute(null);

            dialogs.SaveDialogResult = null; // диалог выбора файла ответил бы отказом
            main.AllOperations.Add(Drill());
            main.SaveProjectCommand.Execute(null);

            main.ConfirmClose();
            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Второе сохранение прошло в тот же файл, изменений не осталось");
        }

        [TestMethod]
        public void SaveAs_AlwaysAsksForFileName()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            main.SaveProjectCommand.Execute(null);

            var otherPath = Path.Combine(Path.GetTempPath(), $"gcodegen_doc_{Guid.NewGuid():N}.ygc");
            dialogs.SaveDialogResult = otherPath;
            try
            {
                main.SaveProjectAsCommand.Execute(null);
                Assert.IsTrue(File.Exists(otherPath), "«Сохранить как» пишет в выбранный файл");
            }
            finally
            {
                if (File.Exists(otherPath))
                    File.Delete(otherPath);
            }
        }

        [TestMethod]
        public void OpeningProject_StartsClean()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveDialogResult = _projectPath;
            main.SaveProjectCommand.Execute(null);
            main.NewProgramCommand.Execute(null);

            dialogs.OpenDialogResult = _projectPath;
            main.OpenProjectCommand.Execute(null);

            Assert.AreEqual(1, main.AllOperations.Count, "Проект открыт");
            main.ConfirmClose();
            Assert.AreEqual(0, dialogs.SaveConfirmationCount,
                "Только что открытый проект несохранённым не считается");
        }

        /// <summary>
        /// Ответ «Отмена» на вопрос о несохранённых изменениях останавливает
        /// и закрытие программы, и создание нового проекта.
        /// </summary>
        [TestMethod]
        public void CancelAnswer_StopsTheAction()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Cancel;

            Assert.IsFalse(main.ConfirmClose(), "Закрытие отменено");

            main.NewProgramCommand.Execute(null);
            Assert.AreEqual(1, main.AllOperations.Count, "Создание нового проекта отменено");
        }

        [TestMethod]
        public void SaveAnswer_WritesFileAndAllowsTheAction()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
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
            main.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Save;
            dialogs.SaveDialogResult = null;

            Assert.IsFalse(main.ConfirmClose(), "Несохранённый проект не закрывается");
        }

        [TestMethod]
        public void DiscardAnswer_AllowsTheActionWithoutSaving()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            main.AllOperations.Add(Drill());
            dialogs.SaveConfirmationResult = SaveConfirmation.Discard;

            Assert.IsTrue(main.ConfirmClose(), "Пользователь согласился потерять изменения");
            Assert.IsFalse(File.Exists(_projectPath), "Ничего не сохранялось");
        }

        /// <summary>
        /// Заголовок окна показывает, с каким файлом идёт работа и есть ли
        /// несохранённые изменения.
        /// </summary>
        [TestMethod]
        public void WindowTitle_ShowsFileNameAndChangeMark()
        {
            var (main, _, dialogs, _) = MainViewModelOperationEditTests.CreateMain();
            StringAssert.Contains(main.DisplayName, "UntitledProject",
                "У несохранённого проекта вместо имени файла — «без имени»");

            main.AllOperations.Add(Drill());
            StringAssert.Contains(main.DisplayName, "*", "Изменения отмечены звёздочкой");

            dialogs.SaveDialogResult = _projectPath;
            main.SaveProjectCommand.Execute(null);

            StringAssert.Contains(main.DisplayName, Path.GetFileName(_projectPath),
                "После сохранения виден файл проекта");
            Assert.IsFalse(main.DisplayName.Contains("*"), "Звёздочка снята");
        }
    }
}
