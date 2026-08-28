using System;
using System.Collections.Generic;
using GCodeGenerator.Services;

namespace GCodeGenerator.Tests.Fixtures
{
    /// <summary>
    /// Диалоги в тестах: ничего не показывает, но помнит, о чём его просили.
    ///
    /// Прежде такая подделка была написана пять раз — в каждом файле тестов,
    /// которому нужна была view-модель с диалогами. Отличались они одним-двумя
    /// полями, а расходились при каждой правке контракта; теперь подделка одна
    /// и реализует все три диалоговых контракта сразу — тест передаёт её туда,
    /// где нужен любой из них.
    /// </summary>
    public sealed class FakeDialogs : IMessageService, IFileDialogService, IDialogHost
    {
        /// <summary>Путь, который «выбирает» пользователь при открытии файла (null — отмена).</summary>
        public string OpenDialogResult { get; set; }

        /// <summary>Путь, который «выбирает» пользователь при сохранении (null — отмена).</summary>
        public string SaveDialogResult { get; set; }

        /// <summary>Ответ на вопрос о несохранённых изменениях.</summary>
        public SaveConfirmation SaveConfirmationResult { get; set; } = SaveConfirmation.Discard;

        /// <summary>Сколько раз спросили о несохранённых изменениях.</summary>
        public int SaveConfirmationCount { get; private set; }

        /// <summary>Последнее сообщение об ошибке и его заголовок.</summary>
        public string LastErrorMessage { get; private set; }

        public string LastErrorTitle { get; private set; }

        /// <summary>Последнее сообщение, показанное как справочное.</summary>
        public string LastInfoMessage { get; private set; }

        /// <summary>Сколько справочных сообщений было показано.</summary>
        public int InfoMessageCount { get; private set; }

        /// <summary>Все показанные view-модели окон в порядке показа.</summary>
        public List<object> ShownDialogs { get; } = new List<object>();

        /// <summary>Последняя показанная view-модель окна.</summary>
        public object ShownViewModel => ShownDialogs.Count == 0 ? null : ShownDialogs[ShownDialogs.Count - 1];

        /// <summary>
        /// Что делает «пользователь» с открытым окном: нажимает OK, правит
        /// параметр, закрывает. Вызывается при показе.
        /// </summary>
        public Action<object> DialogAction { get; set; }

        /// <summary>Ошибку можно объявить провалом теста — тогда её замечают сразу.</summary>
        public Action<string> OnError { get; set; }

        public void ShowInfo(string message, string title = "")
        {
            LastInfoMessage = message;
            InfoMessageCount++;
        }

        /// <summary>Сколько раз показывали сообщение об ошибке.</summary>
        public int ErrorMessageCount { get; private set; }

        /// <summary>Сколько раз спрашивали имя файла для открытия.</summary>
        public int OpenDialogCount { get; private set; }

        public void ShowError(string message, string title = "")
        {
            LastErrorMessage = message;
            LastErrorTitle = title;
            ErrorMessageCount++;
            OnError?.Invoke(message);
        }

        public SaveConfirmation ShowSaveConfirmation(string message, string title = "")
        {
            SaveConfirmationCount++;
            return SaveConfirmationResult;
        }

        public string ShowOpenDialog(string title, string filter, string defaultExtension = "")
        {
            OpenDialogCount++;
            return OpenDialogResult;
        }

        public string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "")
            => SaveDialogResult;

        public void ShowDialog(object viewModel)
        {
            ShownDialogs.Add(viewModel);
            DialogAction?.Invoke(viewModel);
        }
    }
}
