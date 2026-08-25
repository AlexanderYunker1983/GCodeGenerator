namespace GCodeGenerator.Services
{
    /// <summary>
    /// Выбор файла пользователем: открыть или сохранить. Сам файл сервис
    /// не читает и не пишет — это дело файловых сервисов, — а только
    /// возвращает выбранный путь.
    /// </summary>
    public interface IFileDialogService
    {
        /// <summary>Диалог открытия файла. Возвращает путь или null, если отменено.</summary>
        string ShowOpenDialog(string title, string filter, string defaultExtension = "");

        /// <summary>Диалог сохранения файла. Возвращает путь или null, если отменено.</summary>
        string ShowSaveDialog(string title, string filter, string defaultExtension = "", string fileName = "");
    }
}
