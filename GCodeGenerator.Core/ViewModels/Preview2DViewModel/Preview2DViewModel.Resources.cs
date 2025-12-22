using GCodeGenerator.Core.Attributes;
using GCodeGenerator.Core.Localization;

namespace GCodeGenerator.Core.ViewModels.Preview2DViewModel;

public partial class Preview2DViewModel
{
    [Localized]
    public string DisplayName => Resources.GetString("Preview2D_DisplayName");
}

