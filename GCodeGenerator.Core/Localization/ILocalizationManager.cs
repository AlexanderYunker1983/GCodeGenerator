#nullable enable
using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace GCodeGenerator.Localization
{
    public interface ILocalizationManager
    {
        void ChangeCulture(CultureInfo cultureInfo);
        string GetString(string key, params object[] parameters);
        void AddResourceManager(ResourceManager resourceManager);
        void AddAssembly(Assembly assembly, string resourcePath = "Resources.LocalizableResources");
        void AddAssembly(string assemblyName, string resourcePath = "Resources.LocalizableResources");
        event EventHandler? CultureChanged;
    }
}
