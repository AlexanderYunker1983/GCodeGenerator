using System;

namespace GCodeGenerator.Services
{
    /// <summary>
    /// Пункт 7.5 плана: переключение темы через IoC (ранее статика
    /// <c>ThemeHelper</c>).
    /// </summary>
    public interface IThemeService
    {
        /// <summary>Событие после переключения темы приложения.</summary>
        event EventHandler ThemeChanged;

        /// <summary>Применяет тему (тёмную или светлую).</summary>
        void ApplyTheme(bool useDarkTheme);
    }
}
