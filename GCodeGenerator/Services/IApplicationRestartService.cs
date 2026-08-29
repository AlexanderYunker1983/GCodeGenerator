#nullable enable
namespace GCodeGenerator.Services
{
    /// <summary>
    /// Регистрирует процесс в Windows Restart Manager для безопасного
    /// перезапуска после обновления установленного экземпляра.
    /// </summary>
    public interface IApplicationRestartService
    {
        /// <summary>
        /// Обновляет командную строку перезапуска. Сохранённый проект будет
        /// открыт снова; null перезапускает приложение с пустым документом.
        /// </summary>
        void Register(string? projectFile);
    }
}
