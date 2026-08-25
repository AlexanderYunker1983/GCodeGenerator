namespace GCodeGenerator.Models
{
    /// <summary>
    /// UI settings (theme). Не влияют на генерацию G-кода.
    /// Пункт 8.1 плана: выделено из плоского <see cref="GCodeSettings"/>.
    /// </summary>
    public class UiSettings
    {
        /// <summary>
        /// Enables dark (night) MahApps theme across the app.
        /// </summary>
        public bool UseDarkTheme { get; set; }

        /// <summary>
        /// Язык интерфейса: код культуры («ru», «en») либо пустая строка —
        /// брать язык системы.
        ///
        /// Прежде язык выбирался только системой, и сменить его иначе как
        /// сменой языка Windows было нельзя, хотя переводов у программы два.
        /// </summary>
        public string Language { get; set; } = string.Empty;
    }
}
