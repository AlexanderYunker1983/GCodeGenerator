using System;
using System.Windows;
using ControlzEx.Theming;
using MahApps.Metro.Theming;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Helper for switching MahApps themes at runtime.
    /// MahApps 2.x: ThemeManager перенесён из MahApps.Metro в ControlzEx
    /// (ControlzEx.Theming.ThemeManager.Current); темы MahApps регистрируются
    /// через MahAppsLibraryThemeProvider (регистрация идемпотентна).
    /// </summary>
    public static class ThemeHelper
    {
        public static event EventHandler ThemeChanged;

        public static void ApplyTheme(bool useDarkTheme)
        {
            var application = Application.Current;
            if (application == null)
                return;

            // MahApps 2.x: регистрируем провайдер тем библиотеки (идемпотентно).
            ThemeManager.Current.RegisterLibraryThemeProvider(MahAppsLibraryThemeProvider.DefaultInstance);

            var baseColor = useDarkTheme ? ThemeManager.BaseColorDark : ThemeManager.BaseColorLight;

            // Try to get configured accent; fall back to current detected color scheme.
            string colorScheme = "Blue";
            if (ThemeManager.Current.GetTheme(baseColor, colorScheme) == null)
            {
                var detected = ThemeManager.Current.DetectTheme(application);
                if (detected == null)
                    return;
                colorScheme = detected.ColorScheme;
            }

            // ChangeTheme подменяет словарь темы в Application.Resources
            // (Styles/Themes/{Light|Dark}.{Accent}.xaml) — как в 1.x ChangeAppStyle.
            ThemeManager.Current.ChangeTheme(application, baseColor, colorScheme);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
