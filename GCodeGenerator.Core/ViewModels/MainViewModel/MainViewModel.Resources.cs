using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.MainViewModel;

public partial class MainViewModel
{
    [Localized]
    public string DisplayName => string.Format(Resources.Main_DisplayName, BuildInfo.GitTag);
    
    [Localized]
    public string MenuSettingsText => Resources.GetString("Menu_Settings");
    
    [Localized]
    public string MenuSettingsApplicationText => Resources.GetString("Menu_Settings_Application");
}

