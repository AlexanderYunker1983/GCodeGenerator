using System.Reflection;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces;
using MugenMvvmToolkit.Interfaces.Models;
using MugenMvvmToolkit.Models.IoC;
using YLocalization;
using YMugenExtensions;

namespace GCodeGenerator
{
    /// <summary>
    /// Registers localization manager and connects it with GCodeGenerator resources for $i18n bindings.
    /// </summary>
    public class LocalizationModule : IModule
    {
        public bool Load(IModuleContext context)
        {
            var ioc = context.IocContainer;

            if (!ioc.CanResolve<ILocalizationManager>())
                ioc.Bind<ILocalizationManager, MugenLocalizationManager>(DependencyLifecycle.SingleInstance);

            var localizationManager = ioc.Get<ILocalizationManager>();
            localizationManager.AddAssembly("GCodeGenerator");

            // Set ProgramVersion from assembly version
            var version = Assembly.GetAssembly(GetType()).GetName().Version;
            PlatformVariables.ProgramVersion = version.Build == 0 
                ? $"{version.Major}.{version.Minor}" 
                : $"{version.Major}.{version.Minor}.{version.Build}-Developer Version";
            PlatformVariables.LocalizationManager = localizationManager;

            return true;
        }

        public void Unload(IModuleContext context)
        {
        }

        public int Priority => ApplicationSettings.ModulePriorityDefault - 1;
    }
}


