using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using GCodeGenerator.Infrastructure;
using GCodeGenerator.Models;
using MugenMvvmToolkit;
using MugenMvvmToolkit.Interfaces;
using MugenMvvmToolkit.Models;
using MugenMvvmToolkit.WPF.Infrastructure;

namespace GCodeGenerator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            // ReSharper disable once ObjectCreationAsStatement
            new BootstrapperEx(this, new AutofacContainer());
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeHelper.ApplyTheme(GCodeSettingsStore.Current.UseDarkTheme);
        }
    }

    public class BootstrapperEx : Bootstrapper<GCodeGeneratorMvvmApp>
    {
        public BootstrapperEx(Application application, IIocContainer iocContainer,
            IEnumerable<Assembly> assemblies = null, PlatformInfo platform = null)
            : base(application, iocContainer, assemblies, platform)
        {
        }
    }
}
