using System.Collections.Generic;
using System.Reflection;
using System.Windows;
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
