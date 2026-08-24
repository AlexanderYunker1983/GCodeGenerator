using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace GCodeGenerator.Localization
{
    public class LocalizationManager : ILocalizationManager
    {
        /// <summary>
        /// Cached ResourceManagers for each ResourceSet supported requested.       
        /// </summary>
        private readonly List<ResourceManager> _resourceManagers = new List<ResourceManager>();

        private CultureInfo _culture;
        public void AddAssembly(string assemblyName, string resourcePath = "Resources.LocalizableResources")
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            AddAssembly(assembly, resourcePath);
        }

        public event EventHandler CultureChanged;

        public CultureInfo Culture
        {
            get => _culture;
            set
            {
                if (!Equals(_culture, value))
                {
                    _culture = value;
                    CultureChanged?.Invoke(this, EventArgs.Empty);
                }

            }
        }

        public virtual void ChangeCulture(CultureInfo cultureInfo)
        {
            Culture = cultureInfo;
        }

        public string GetString(string key, params object[] parameters)
        {
            foreach (var resourceManager in _resourceManagers)
            {
                var str = GetString(resourceManager, key, Culture, parameters);
                if (!string.IsNullOrEmpty(str)) return str;
            }

            // Пункт 8.3 плана: отсутствующий ключ → лог + сам ключ в виде
            // «?key?» (захардкоженные фолбэки в VM удалены).
            LogMissingKey(key);
            return $"?{key}?";
        }

        /// <summary>
        /// Пункт 8.3 плана: логирование отсутствующего ключа локализации.
        /// Минимальная реализация — System.Diagnostics.Debug (Core — чистый BCL,
        /// лог-фреймворк не введён); в приложении виден в Output-окне VS/DebugView.
        /// </summary>
        protected virtual void LogMissingKey(string key)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Localization] Missing localization key: {key} (culture: {Culture?.Name ?? "CurrentUICulture"})");
        }

        private static string GetString(ResourceManager manager, string stringName, CultureInfo cultureInfo, params object[] parameters)
        {
            if (manager == null) return null;
            string str;
            var unFormattedString = string.Empty;
            try
            {
                unFormattedString = manager.GetString(stringName, cultureInfo) ?? String.Empty;
            }
            catch (MissingManifestResourceException)
            {
            }
            catch (NullReferenceException)
            {
            }
            try
            {
                str = string.Format(unFormattedString, parameters);
            }
            catch (FormatException)
            {
                str = unFormattedString;
            }
            return str;
        }

        public void AddResourceManager(ResourceManager resourceManager)
        {
            if (resourceManager != null) _resourceManagers.Add(resourceManager);
        }

        public void AddAssembly(Assembly assembly, string resourcePath = "Resources.LocalizableResources")
        {
            var resName = GetDefaultResourceName(assembly.ManifestModule.Name, resourcePath);
            _resourceManagers.Add(new ResourceManager(resName, assembly));
        }
        private static string GetDefaultResourceName(string assemblyModuleName, string resourcePath)
        {
            string stringResourceName;
            if (assemblyModuleName.ToLower().Contains(".exe"))
            {
                stringResourceName = $"{assemblyModuleName.Remove(assemblyModuleName.ToLower().LastIndexOf(".exe", StringComparison.Ordinal))}.{resourcePath}";
                return stringResourceName;
            }
            stringResourceName = $"{assemblyModuleName.Remove(assemblyModuleName.ToLower().LastIndexOf(".dll", StringComparison.Ordinal))}.{resourcePath}";
            return stringResourceName;
        }

    }
}
