using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.RightPanel.GCodeViewModel;

public partial class GCodeViewModel
{
    [Localized]
    public string DisplayName => Resources.GetString("RightPanel_GCode_DisplayName");
}


