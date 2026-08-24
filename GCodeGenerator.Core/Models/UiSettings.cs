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
    }
}
