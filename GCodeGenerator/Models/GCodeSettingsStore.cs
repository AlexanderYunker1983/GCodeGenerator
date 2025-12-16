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
                SpindleDelaySeconds = settings.SpindleDelaySeconds,
                CoolantControlEnabled = settings.CoolantControlEnabled,
                CoolantStartEnabled = settings.CoolantStartEnabled,
                CoolantStopEnabled = settings.CoolantStopEnabled,
                AddStartPosition = settings.AddStartPosition,
                StartX = settings.StartX,
                StartY = settings.StartY,
                StartZ = settings.StartZ,
                AddEndPosition = settings.AddEndPosition,
                EndX = settings.EndX,
                EndY = settings.EndY,
                EndZ = settings.EndZ,
                SetWorkCoordinateSystem = settings.SetWorkCoordinateSystem,
                WorkCoordinateSystem = settings.WorkCoordinateSystem ?? "G54"
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
            Properties.Settings.Default.CoolantControlEnabled = Current.CoolantControlEnabled;
            Properties.Settings.Default.CoolantStartEnabled = Current.CoolantStartEnabled;
            Properties.Settings.Default.CoolantStopEnabled = Current.CoolantStopEnabled;
            Properties.Settings.Default.AddStartPosition = Current.AddStartPosition;
            Properties.Settings.Default.StartX = Current.StartX;
            Properties.Settings.Default.StartY = Current.StartY;
            Properties.Settings.Default.StartZ = Current.StartZ;
            Properties.Settings.Default.AddEndPosition = Current.AddEndPosition;
            Properties.Settings.Default.EndX = Current.EndX;
            Properties.Settings.Default.EndY = Current.EndY;
            Properties.Settings.Default.EndZ = Current.EndZ;
            Properties.Settings.Default.SetWorkCoordinateSystem = Current.SetWorkCoordinateSystem;
            Properties.Settings.Default.WorkCoordinateSystem = Current.WorkCoordinateSystem ?? "G54";
            Properties.Settings.Default.Save();
        }
    }
}


