namespace GCodeGenerator.Models
{
    /// <summary>
    /// Simple in-memory storage for shared G-code settings.
    /// Later it can be replaced with persistent storage (Properties.Settings, config file, etc.).
    /// </summary>
    public static class GCodeSettingsStore
    {
        public static GCodeSettings Current { get; } = new GCodeSettings();
    }
}


