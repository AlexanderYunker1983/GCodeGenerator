using GCodeGenerator.Properties;

namespace GCodeGenerator.Models
{
    /// <summary>
    /// Shared settings storage with simple persistence layer (Properties.Settings).
    /// Only persists values that need to survive app restarts.
    /// </summary>
    public static class GCodeSettingsStore
    {
        static GCodeSettingsStore()
        {
            // Initialize from persistent storage
            var settings = Properties.Settings.Default;
            Current = new GCodeSettings
            {
                UseLineNumbers = true,
                LineNumberStart = 10,
                LineNumberStep = 10,
                UseComments = true,
                AllowArcs = true,
                UsePaddedGCodes = false,
                UseDarkTheme = settings.UseDarkTheme
            };
        }

        public static GCodeSettings Current { get; }

        public static void Save()
        {
            // Persist only fields that should survive restarts
            Properties.Settings.Default.UseDarkTheme = Current.UseDarkTheme;
            Properties.Settings.Default.Save();
        }
    }
}


