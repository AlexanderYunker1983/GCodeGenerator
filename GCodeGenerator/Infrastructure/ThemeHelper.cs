using System;
using System.Windows;
using MahApps.Metro;

namespace GCodeGenerator.Infrastructure
{
    /// <summary>
    /// Helper for switching MahApps themes at runtime.
    /// </summary>
    public static class ThemeHelper
    {
        public static event EventHandler ThemeChanged;

        public static void ApplyTheme(bool useDarkTheme)
        {
            var application = Application.Current;
            if (application == null)
                return;

            // Try to get configured accent; fall back to current detected accent.
            var accent = ThemeManager.GetAccent("Blue") ?? ThemeManager.DetectAppStyle(application)?.Item2;

            var appTheme = ThemeManager.GetAppTheme(useDarkTheme ? "BaseDark" : "BaseLight")
                           ?? ThemeManager.DetectAppStyle(application)?.Item1;

            if (accent != null && appTheme != null)
            {
                ThemeManager.ChangeAppStyle(application, accent, appTheme);
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}

