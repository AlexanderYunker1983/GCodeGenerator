#nullable enable
namespace GCodeGenerator.Services
{
    /// <summary>
    /// Ответ на вопрос о несохранённых изменениях.
    /// </summary>
    public enum SaveConfirmation
    {
        /// <summary>Сохранить изменения и продолжить.</summary>
        Save,

        /// <summary>Продолжить, потеряв изменения.</summary>
        Discard,

        /// <summary>Ничего не делать: действие отменено.</summary>
        Cancel
    }

    /// <summary>
    /// Сообщения пользователю: то, что раньше делалось прямым вызовом
    /// <c>MessageBox</c> из view-модели.
    ///
    /// Отделено от выбора файлов и от показа окон намеренно: диалог
    /// импорта чертежа сообщает об ошибке разбора файла, но открывать
    /// произвольные окна ему незачем — а с общим контрактом мог.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>Информационное сообщение (кнопка OK).</summary>
        void ShowInfo(string message, string title = "");

        /// <summary>Сообщение об ошибке (кнопка OK, иконка ошибки).</summary>
        void ShowError(string message, string title = "");

        /// <summary>Обычный вопрос Да/Нет.</summary>
        bool ShowConfirmation(string message, string title = "");

        /// <summary>
        /// Вопрос о несохранённых изменениях (Да/Нет/Отмена): сохранить,
        /// потерять изменения или отменить само действие.
        /// </summary>
        SaveConfirmation ShowSaveConfirmation(string message, string title = "");
    }
}
