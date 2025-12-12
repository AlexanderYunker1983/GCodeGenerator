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
                UseLineNumbers = settings.UseLineNumbers,
                LineNumberStart = settings.LineNumberStart,
                LineNumberStep = settings.LineNumberStep,
                UseComments = settings.UseComments,
                AllowArcs = settings.AllowArcs,
                UsePaddedGCodes = settings.UsePaddedGCodes,
                UseDarkTheme = settings.UseDarkTheme,
                SpindleControlEnabled = settings.SpindleControlEnabled,
                SpindleSpeedEnabled = settings.SpindleSpeedEnabled,
                SpindleSpeedRpm = settings.SpindleSpeedRpm,
                SpindleStartEnabled = settings.SpindleStartEnabled,
                SpindleStartCommand = settings.SpindleStartCommand,
                SpindleStopEnabled = settings.SpindleStopEnabled,
                SpindleDelayEnabled = settings.SpindleDelayEnabled,
                SpindleDelaySeconds = settings.SpindleDelaySeconds
            };
        }

        public static GCodeSettings Current { get; }

        public static void Save()
        {
            // Persist only fields that should survive restarts
            Properties.Settings.Default.UseLineNumbers = Current.UseLineNumbers;
            Properties.Settings.Default.LineNumberStart = Current.LineNumberStart;
            Properties.Settings.Default.LineNumberStep = Current.LineNumberStep;
            Properties.Settings.Default.UseComments = Current.UseComments;
            Properties.Settings.Default.AllowArcs = Current.AllowArcs;
            Properties.Settings.Default.UsePaddedGCodes = Current.UsePaddedGCodes;
            Properties.Settings.Default.UseDarkTheme = Current.UseDarkTheme;
            Properties.Settings.Default.SpindleControlEnabled = Current.SpindleControlEnabled;
            Properties.Settings.Default.SpindleSpeedEnabled = Current.SpindleSpeedEnabled;
            Properties.Settings.Default.SpindleSpeedRpm = Current.SpindleSpeedRpm;
            Properties.Settings.Default.SpindleStartEnabled = Current.SpindleStartEnabled;
            Properties.Settings.Default.SpindleStartCommand = Current.SpindleStartCommand;
            Properties.Settings.Default.SpindleStopEnabled = Current.SpindleStopEnabled;
            Properties.Settings.Default.SpindleDelayEnabled = Current.SpindleDelayEnabled;
            Properties.Settings.Default.SpindleDelaySeconds = Current.SpindleDelaySeconds;
            Properties.Settings.Default.Save();
        }
    }
}


